using BioAnalyzer.Research.Api.Domain.Clients;
using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Services;

public class LiteratureExpansionService(
    ILiteratureSearchService literatureSearchService,
    IStorageClient storageClient,
    IOptions<LiteratureExpansionConfiguration> expansionOptions,
    ILogger<LiteratureExpansionService> logger) : ILiteratureExpansionService
{
    private readonly LiteratureExpansionConfiguration _config = expansionOptions.Value;
    private readonly ILogger<LiteratureExpansionService> _logger = logger;

    public async Task<LiteratureCandidatesResult> FindCandidatesAsync(
        LiteratureCandidatesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Query cannot be null or empty.", nameof(request));
        }

        var query = request.Query.Trim();
        var result = new LiteratureCandidatesResult
        {
            Query = query,
            Enabled = _config.Enabled
        };

        if (!_config.Enabled)
        {
            result.SkippedReasons.Add("Literature expansion is disabled by configuration.");
            return result;
        }

        var maxCandidates = request.MaxCandidates > 0
            ? Math.Min(request.MaxCandidates, _config.MaxCandidates)
            : _config.MaxCandidates;
        var requirePmcId = request.RequirePmcId ?? _config.RequirePmcId;
        var requireOpenAccessLink = request.RequireOpenAccessLink ?? _config.RequireOpenAccessLink;

        var search = await literatureSearchService
            .SearchLiteratureAsync(query, startIndex: 0, retMax: _config.SearchRetMax, cancellationToken)
            .ConfigureAwait(false);

        var idList = search.IdList?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
                     ?? [];
        result.SearchHitCount = int.TryParse(search.Count, out var count) ? count : idList.Count;

        if (idList.Count == 0)
        {
            result.SkippedReasons.Add("Entrez search returned no PubMed IDs.");
            return result;
        }

        var summaries = await literatureSearchService
            .GetLiteratureSummaries(idList, cancellationToken)
            .ConfigureAwait(false);
        result.SummariesConsidered = summaries.Count;

        var pmcSummaries = new List<EntrezSummaryResult>();
        foreach (var summary in summaries)
        {
            if (requirePmcId && string.IsNullOrWhiteSpace(summary.PmcId))
            {
                continue;
            }

            pmcSummaries.Add(summary);
        }

        if (pmcSummaries.Count == 0)
        {
            result.SkippedReasons.Add(
                requirePmcId
                    ? "No search hits included a PMCID."
                    : "No summaries available after search.");
            return result;
        }

        HashSet<string> ingestedPmcIds;
        try
        {
            var downloads = await storageClient.GetDownloadsAsync(cancellationToken).ConfigureAwait(false);
            ingestedPmcIds = BuildIngestedPmcIdSet(downloads.Downloads);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load existing downloads for candidate dedupe; continuing without dedupe.");
            ingestedPmcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            result.SkippedReasons.Add("Could not load existing downloads for alreadyIngested detection.");
        }

        var resolutionTargets = pmcSummaries
            .Take(Math.Max(maxCandidates * 3, maxCandidates))
            .ToList();

        var resolved = await ResolveLinksAsync(resolutionTargets, requireOpenAccessLink, cancellationToken)
            .ConfigureAwait(false);
        result.LinkResolutionsAttempted = resolutionTargets.Count;

        var candidates = new List<LiteratureCandidate>();
        var rank = 1;
        foreach (var item in resolved)
        {
            if (candidates.Count >= maxCandidates)
            {
                break;
            }

            var normalizedPmc = NormalizePmcId(item.Summary.PmcId);
            var alreadyIngested = ingestedPmcIds.Contains(normalizedPmc)
                                  || ingestedPmcIds.Contains(StripPmcPrefix(normalizedPmc));

            candidates.Add(new LiteratureCandidate
            {
                Rank = rank++,
                Pmid = item.Summary.Uid ?? string.Empty,
                PmcId = normalizedPmc,
                Title = item.Summary.Title ?? string.Empty,
                Doi = item.Summary.Doi ?? string.Empty,
                PdfLink = item.PdfLink,
                XmlLink = item.XmlLink,
                AlreadyIngested = alreadyIngested
            });
        }

        result.Candidates = candidates;
        _logger.LogInformation(
            "Literature candidates resolved. QueryLength={QueryLength} SearchHits={SearchHits} Summaries={Summaries} LinkAttempts={LinkAttempts} Candidates={Candidates} Enabled={Enabled}",
            query.Length,
            result.SearchHitCount,
            result.SummariesConsidered,
            result.LinkResolutionsAttempted,
            candidates.Count,
            result.Enabled);
        if (candidates.Count == 0)
        {
            result.SkippedReasons.Add(
                requireOpenAccessLink
                    ? "No candidates with resolvable PMC Open Access PDF/XML links."
                    : "No candidates remained after filtering.");
        }

        return result;
    }

    private async Task<IReadOnlyList<ResolvedCandidate>> ResolveLinksAsync(
        IReadOnlyList<EntrezSummaryResult> summaries,
        bool requireOpenAccessLink,
        CancellationToken cancellationToken)
    {
        var output = new List<ResolvedCandidate>();
        using var gate = new SemaphoreSlim(_config.MaxConcurrentLinkResolutions, _config.MaxConcurrentLinkResolutions);
        var tasks = summaries.Select(async summary =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var pmcId = summary.PmcId;
                if (string.IsNullOrWhiteSpace(pmcId))
                {
                    return (ResolvedCandidate?)null;
                }

                try
                {
                    var links = await literatureSearchService
                        .GetLiteratureDownloadLinkAsync(pmcId, cancellationToken)
                        .ConfigureAwait(false);

                    var pdfLink = links.PdfLink?.Trim() ?? string.Empty;
                    var xmlLink = links.ArchiveLink?.Trim() ?? string.Empty;
                    var hasLink = !string.IsNullOrWhiteSpace(pdfLink) || !string.IsNullOrWhiteSpace(xmlLink);
                    if (requireOpenAccessLink && !hasLink)
                    {
                        return null;
                    }

                    return new ResolvedCandidate(summary, pdfLink, xmlLink);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "OA link resolution failed for PMCID {PmcId}", pmcId);
                    return requireOpenAccessLink ? null : new ResolvedCandidate(summary, string.Empty, string.Empty);
                }
            }
            finally
            {
                gate.Release();
            }
        });

        var resolved = await Task.WhenAll(tasks).ConfigureAwait(false);
        // Preserve Entrez/summary order
        foreach (var item in resolved)
        {
            if (item is not null)
            {
                output.Add(item);
            }
        }

        return output;
    }

    private static HashSet<string> BuildIngestedPmcIdSet(IList<LiteratureDownload> downloads)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var download in downloads)
        {
            if (string.IsNullOrWhiteSpace(download.PmcId))
            {
                continue;
            }

            var normalized = NormalizePmcId(download.PmcId);
            set.Add(normalized);
            set.Add(StripPmcPrefix(normalized));
        }

        return set;
    }

    private static string NormalizePmcId(string pmcId)
    {
        if (string.IsNullOrWhiteSpace(pmcId))
        {
            return string.Empty;
        }

        var trimmed = pmcId.Trim();
        return trimmed.StartsWith("PMC", StringComparison.OrdinalIgnoreCase)
            ? "PMC" + trimmed[3..]
            : "PMC" + trimmed;
    }

    private static string StripPmcPrefix(string pmcId)
    {
        if (string.IsNullOrWhiteSpace(pmcId))
        {
            return string.Empty;
        }

        return pmcId.StartsWith("PMC", StringComparison.OrdinalIgnoreCase)
            ? pmcId[3..]
            : pmcId;
    }

    private sealed record ResolvedCandidate(EntrezSummaryResult Summary, string PdfLink, string XmlLink);
}
