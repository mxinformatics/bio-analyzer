using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.Research.Api.Domain.Clients;
using BioAnalyzer.Research.Api.Domain.DataTransfer;
using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Services;

public class LiteratureIngestService(
    ILiteratureSearchService literatureSearchService,
    IStorageClient storageClient,
    IEventBusClient eventBusClient,
    ITableContext tableContext,
    IOptions<ResearchApiStorageConfiguration> storageOptions,
    IOptions<LiteratureExpansionConfiguration> expansionOptions,
    ILogger<LiteratureIngestService> logger) : ILiteratureIngestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ResearchApiStorageConfiguration _storage = storageOptions.Value;
    private readonly LiteratureExpansionConfiguration _expansion = expansionOptions.Value;
    private readonly ILogger<LiteratureIngestService> _logger = logger;

    public async Task<LiteratureIngestResponse> RequestIngestAsync(
        LiteratureIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ArgumentException("At least one ingest item is required.", nameof(request));
        }

        if (!_expansion.AutoIngestEnabled)
        {
            throw new InvalidOperationException("Automatic literature ingest is disabled (LiteratureExpansion:AutoIngestEnabled=false).");
        }

        // Controller already overwrote RequestedBy from principal; normalize only.
        var requester = NormalizeRequester(request.RequestedBy);
        EnsureRequesterAllowed(requester, operation: "ingest");

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (_expansion.RequireIdempotencyKey && string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(request));
        }

        var maxItems = Math.Max(1, _expansion.MaxCandidates);
        var queryPreviewMax = Math.Max(32, _expansion.QueryPreviewMaxLength);
        var queryPreview = Truncate(request.QueryPreview?.Trim() ?? string.Empty, queryPreviewMax);
        var fingerprint = BuildRequestFingerprint(requester, queryPreview, request.Items.Take(maxItems));

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await TryGetIdempotentResponseAsync(idempotencyKey, fingerprint, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                _logger.LogInformation(
                    "Literature ingest idempotent replay. IdempotencyKeyHash={IdempotencyKeyHash} JobId={JobId} RequestedBy={RequestedBy}",
                    HashForLog(idempotencyKey),
                    existing.JobId,
                    HashForLog(requester));
                existing.Replayed = true;
                return existing;
            }
        }

        await EnsureDailyQuotaAsync(requester, cancellationToken).ConfigureAwait(false);

        var jobId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        HashSet<string> existingPmcIds;
        try
        {
            var downloads = await storageClient.GetDownloadsAsync(cancellationToken).ConfigureAwait(false);
            existingPmcIds = BuildExistingPmcIdSet(downloads.Downloads);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed loading downloads for ingest dedupe; continuing without skip-on-existing.");
            existingPmcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var enqueued = new List<LiteratureIngestEnqueueResult>();
        var skipped = new List<LiteratureIngestEnqueueResult>();
        var busMessages = new List<LiteratureDownloadBusMessage>();
        var itemStatuses = new List<LiteratureIngestItemStatus>();

        foreach (var rawItem in request.Items.Take(maxItems))
        {
            var pmcId = NormalizePmcId(rawItem.PmcId);
            if (string.IsNullOrWhiteSpace(pmcId))
            {
                skipped.Add(new LiteratureIngestEnqueueResult
                {
                    PmcId = rawItem.PmcId ?? string.Empty,
                    Title = rawItem.Title ?? string.Empty,
                    Reason = "Missing or invalid PMCID."
                });
                continue;
            }

            if (existingPmcIds.Contains(pmcId) || existingPmcIds.Contains(StripPmcPrefix(pmcId)))
            {
                skipped.Add(new LiteratureIngestEnqueueResult
                {
                    PmcId = pmcId,
                    Title = rawItem.Title ?? string.Empty,
                    Reason = "Already present in download table."
                });
                itemStatuses.Add(new LiteratureIngestItemStatus
                {
                    PmcId = pmcId,
                    Title = rawItem.Title ?? string.Empty,
                    Status = "Skipped",
                    Stage = "Dedupe",
                    ErrorMessage = "Already present in download table."
                });
                continue;
            }

            var title = rawItem.Title?.Trim() ?? string.Empty;
            var doi = rawItem.Doi?.Trim() ?? string.Empty;
            var downloadLink = rawItem.DownloadLink?.Trim() ?? string.Empty;
            var xmlLink = rawItem.XmlLink?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(downloadLink) && string.IsNullOrWhiteSpace(xmlLink))
            {
                try
                {
                    var links = await literatureSearchService
                        .GetLiteratureDownloadLinkAsync(pmcId, cancellationToken)
                        .ConfigureAwait(false);
                    downloadLink = links.PdfLink?.Trim() ?? string.Empty;
                    xmlLink = links.ArchiveLink?.Trim() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed resolving OA links for {PmcId}", pmcId);
                    skipped.Add(new LiteratureIngestEnqueueResult
                    {
                        PmcId = pmcId,
                        Title = title,
                        Reason = $"Failed to resolve OA download link: {ex.Message}"
                    });
                    itemStatuses.Add(new LiteratureIngestItemStatus
                    {
                        PmcId = pmcId,
                        Title = title,
                        Status = "Failed",
                        Stage = "ResolveLinks",
                        ErrorMessage = ex.Message
                    });
                    continue;
                }
            }

            var selectedLink = !string.IsNullOrWhiteSpace(downloadLink) ? downloadLink : xmlLink;
            if (string.IsNullOrWhiteSpace(selectedLink))
            {
                skipped.Add(new LiteratureIngestEnqueueResult
                {
                    PmcId = pmcId,
                    Title = title,
                    Reason = "No OA PDF/XML link available."
                });
                itemStatuses.Add(new LiteratureIngestItemStatus
                {
                    PmcId = pmcId,
                    Title = title,
                    Status = "Skipped",
                    Stage = "ResolveLinks",
                    ErrorMessage = "No OA PDF/XML link available."
                });
                continue;
            }

            if (_expansion.RequireOpenAccessLink &&
                !selectedLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !selectedLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(new LiteratureIngestEnqueueResult
                {
                    PmcId = pmcId,
                    Title = title,
                    Reason = "Download link is not an HTTP(S) OA URL."
                });
                continue;
            }

            busMessages.Add(new LiteratureDownloadBusMessage
            {
                PmcId = pmcId,
                DownloadLink = selectedLink,
                Title = title,
                Doi = doi,
                JobId = jobId
            });

            enqueued.Add(new LiteratureIngestEnqueueResult
            {
                PmcId = pmcId,
                Title = title,
                Reason = "Enqueued for download.",
                DownloadLink = selectedLink
            });

            itemStatuses.Add(new LiteratureIngestItemStatus
            {
                PmcId = pmcId,
                Title = title,
                Status = LiteratureIngestJobStatus.Accepted,
                Stage = "Queued"
            });

            existingPmcIds.Add(pmcId);
            existingPmcIds.Add(StripPmcPrefix(pmcId));
        }

        var jobStatus = busMessages.Count == 0
            ? (skipped.Count > 0 ? LiteratureIngestJobStatus.Failed : LiteratureIngestJobStatus.Accepted)
            : LiteratureIngestJobStatus.Accepted;

        // Phase D2: durable job (+ idempotency reservation) BEFORE Service Bus publish.
        var job = new LiteratureIngestJobDto
        {
            RowKey = jobId,
            Status = jobStatus,
            CorrelationId = request.CorrelationId?.Trim() ?? string.Empty,
            RequestedBy = requester,
            QueryPreview = queryPreview,
            IdempotencyKey = idempotencyKey,
            ItemsJson = JsonSerializer.Serialize(itemStatuses, JsonOptions),
            ErrorMessage = busMessages.Count == 0 && skipped.Count > 0
                ? "No items were enqueued."
                : string.Empty,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await tableContext
            .UpsertEntityAsync(_storage.IngestJobTableName, job)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            try
            {
                await tableContext.AddEntityAsync(
                        _storage.IngestJobTableName,
                        new LiteratureIngestIdempotencyDto
                        {
                            RowKey = BuildIdempotencyRowKey(idempotencyKey),
                            JobId = jobId,
                            RequestedBy = requester,
                            RequestFingerprint = fingerprint,
                            CreatedUtc = now
                        })
                    .ConfigureAwait(false);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                // Another writer reserved the key first — return their job (or conflict on fingerprint).
                var raced = await TryGetIdempotentResponseAsync(idempotencyKey, fingerprint, cancellationToken)
                    .ConfigureAwait(false);
                if (raced is not null)
                {
                    raced.Replayed = true;
                    return raced;
                }

                throw new InvalidOperationException(
                    "Idempotency key conflict while reserving ingest job; retry the request.");
            }
        }

        if (busMessages.Count > 0)
        {
            try
            {
                await eventBusClient
                    .PublishLiteratureDownloadRequestsAsync(busMessages, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service Bus publish failed after job {JobId} was persisted", jobId);
                job.Status = LiteratureIngestJobStatus.Failed;
                job.ErrorMessage = $"Service Bus publish failed: {ex.Message}";
                job.UpdatedUtc = DateTimeOffset.UtcNow;
                await tableContext.UpsertEntityAsync(_storage.IngestJobTableName, job).ConfigureAwait(false);
                throw;
            }
        }

        _logger.LogInformation(
            "Literature ingest accepted. JobId={JobId} Enqueued={EnqueuedCount} Skipped={SkippedCount} RequestedBy={RequestedBy} QueryPreviewLength={QueryPreviewLength} IdempotencyKeyHash={IdempotencyKeyHash} Fingerprint={Fingerprint}",
            jobId,
            enqueued.Count,
            skipped.Count,
            string.IsNullOrWhiteSpace(requester) ? "anonymous" : HashForLog(requester),
            job.QueryPreview.Length,
            string.IsNullOrWhiteSpace(idempotencyKey) ? "none" : HashForLog(idempotencyKey),
            HashForLog(fingerprint));

        return new LiteratureIngestResponse
        {
            JobId = jobId,
            Status = jobStatus,
            CorrelationId = string.IsNullOrWhiteSpace(job.CorrelationId) ? null : job.CorrelationId,
            Enqueued = enqueued,
            Skipped = skipped,
            Replayed = false
        };
    }

    public async Task<LiteratureIngestJobStatusResult?> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        return await GetJobStatusAsync(jobId, requesterId: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LiteratureIngestJobStatusResult?> GetJobStatusAsync(
        string jobId,
        string? requesterId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("jobId is required.", nameof(jobId));
        }

        var entity = await tableContext
            .GetEntityAsync<LiteratureIngestJobDto>(
                _storage.IngestJobTableName,
                LiteratureIngestJobDto.DefaultPartitionKey,
                jobId.Trim())
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        if (_expansion.RestrictJobStatusToRequester)
        {
            var requester = NormalizeRequester(requesterId);
            EnsureRequesterAllowed(requester, operation: "job-status");

            // Jobs without RequestedBy (legacy) are visible only when anonymous is explicitly allowed.
            if (!string.IsNullOrWhiteSpace(entity.RequestedBy)
                && !RequesterIdentity.EqualsRequester(entity.RequestedBy, requester))
            {
                _logger.LogWarning(
                    "Ingest job status forbidden for mismatched requester. JobId={JobId}",
                    jobId);
                throw new UnauthorizedAccessException("Requester is not authorized to view this ingest job.");
            }
        }

        var items = DeserializeItems(entity.ItemsJson);
        return new LiteratureIngestJobStatusResult
        {
            JobId = entity.JobId,
            Status = entity.Status,
            CorrelationId = string.IsNullOrWhiteSpace(entity.CorrelationId) ? null : entity.CorrelationId,
            RequestedBy = string.IsNullOrWhiteSpace(entity.RequestedBy) ? null : entity.RequestedBy,
            QueryPreview = string.IsNullOrWhiteSpace(entity.QueryPreview) ? null : entity.QueryPreview,
            CreatedUtc = entity.CreatedUtc,
            UpdatedUtc = entity.UpdatedUtc,
            ErrorMessage = string.IsNullOrWhiteSpace(entity.ErrorMessage) ? null : entity.ErrorMessage,
            Items = items
        };
    }

    private void EnsureRequesterAllowed(string requester, string operation)
    {
        if (!string.IsNullOrWhiteSpace(requester))
        {
            return;
        }

        if (_expansion.AllowAnonymousRequester && !_expansion.RequireRequesterIdentity)
        {
            _logger.LogWarning("Anonymous requester allowed for {Operation} (AllowAnonymousRequester=true).", operation);
            return;
        }

        // Fail closed when identity is required, or when quota is enabled (cannot attribute cost).
        if (_expansion.RequireRequesterIdentity || _expansion.MaxIngestJobsPerRequesterPerDay > 0)
        {
            throw new UnauthorizedAccessException(
                "A verified requester identity is required. Authenticate with Entra ID (user or daemon token) or a configured API key.");
        }
    }

    private async Task EnsureDailyQuotaAsync(string requester, CancellationToken cancellationToken)
    {
        if (_expansion.MaxIngestJobsPerRequesterPerDay <= 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(requester))
        {
            // Should have been rejected by EnsureRequesterAllowed; fail closed defensively.
            throw new UnauthorizedAccessException(
                "Cannot enforce ingest quota without a verified requester identity.");
        }

        var allJobs = await tableContext
            .GetAllAsync<LiteratureIngestJobDto>(_storage.IngestJobTableName)
            .ConfigureAwait(false);

        var startOfDay = DateTimeOffset.UtcNow.Date;
        var count = allJobs.Count(job =>
            string.Equals(job.RequestedBy, requester, StringComparison.Ordinal) &&
            job.CreatedUtc >= startOfDay &&
            !string.Equals(job.PartitionKey, LiteratureIngestIdempotencyDto.DefaultPartitionKey, StringComparison.Ordinal));

        if (count >= _expansion.MaxIngestJobsPerRequesterPerDay)
        {
            _logger.LogWarning(
                "Ingest daily quota exceeded. RequestedBy={RequestedBy} Count={Count} Limit={Limit}",
                HashForLog(requester),
                count,
                _expansion.MaxIngestJobsPerRequesterPerDay);
            throw new InvalidOperationException(
                $"Ingest job daily quota exceeded ({_expansion.MaxIngestJobsPerRequesterPerDay} per requester).");
        }
    }

    private async Task<LiteratureIngestResponse?> TryGetIdempotentResponseAsync(
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var map = await tableContext
            .GetEntityAsync<LiteratureIngestIdempotencyDto>(
                _storage.IngestJobTableName,
                LiteratureIngestIdempotencyDto.DefaultPartitionKey,
                BuildIdempotencyRowKey(idempotencyKey))
            .ConfigureAwait(false);

        if (map is null || string.IsNullOrWhiteSpace(map.JobId))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(map.RequestFingerprint)
            && !string.IsNullOrWhiteSpace(requestFingerprint)
            && !string.Equals(map.RequestFingerprint, requestFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Idempotency-Key was reused with a different request body (fingerprint mismatch).");
        }

        var job = await GetJobStatusAsync(map.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        return new LiteratureIngestResponse
        {
            JobId = job.JobId,
            Status = job.Status,
            CorrelationId = job.CorrelationId,
            Enqueued = job.Items
                .Where(i => string.Equals(i.Status, LiteratureIngestJobStatus.Accepted, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Status, "Queued", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Stage, "Queued", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Status, LiteratureIngestJobStatus.Downloading, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Status, LiteratureIngestJobStatus.Processing, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Status, LiteratureIngestJobStatus.GraphReady, StringComparison.OrdinalIgnoreCase))
                .Select(i => new LiteratureIngestEnqueueResult
                {
                    PmcId = i.PmcId,
                    Title = i.Title,
                    Reason = "Idempotent replay of prior enqueue."
                })
                .ToList(),
            Skipped = job.Items
                .Where(i => string.Equals(i.Status, "Skipped", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                .Select(i => new LiteratureIngestEnqueueResult
                {
                    PmcId = i.PmcId,
                    Title = i.Title,
                    Reason = i.ErrorMessage ?? i.Status
                })
                .ToList(),
            Replayed = true
        };
    }

    private static List<LiteratureIngestItemStatus> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<LiteratureIngestItemStatus>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static HashSet<string> BuildExistingPmcIdSet(IList<LiteratureDownload> downloads)
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

    private static string NormalizePmcId(string? pmcId)
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

    private static string StripPmcPrefix(string pmcId) =>
        pmcId.StartsWith("PMC", StringComparison.OrdinalIgnoreCase) ? pmcId[3..] : pmcId;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string NormalizeRequester(string? requester)
    {
        if (string.IsNullOrWhiteSpace(requester))
        {
            return string.Empty;
        }

        return Truncate(requester.Trim(), 200);
    }

    private static string NormalizeIdempotencyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return Truncate(key.Trim(), 200);
    }

    private static string BuildIdempotencyRowKey(string idempotencyKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey));
        return Convert.ToHexString(hash);
    }

    internal static string BuildRequestFingerprint(
        string requester,
        string queryPreview,
        IEnumerable<LiteratureIngestItem> items)
    {
        var pmcIds = items
            .Select(i => NormalizePmcId(i.PmcId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var material = string.Join('|', new[]
        {
            requester ?? string.Empty,
            queryPreview ?? string.Empty,
            string.Join(',', pmcIds)
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash);
    }

    private static string HashForLog(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
