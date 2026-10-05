using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Domain.Parsers;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public class NcbiClient(HttpClient httpClient, INcbiXmlParser xmlParser) : INcbiClient
{
    private const int MaxTooManyRequestsRetries = 2;
    private const string PmcOaOpenDataHttpsBaseUrl = "https://pmc-oa-opendata.s3.amazonaws.com";
    private const string PmcOaOpenDataS3Prefix = "s3://pmc-oa-opendata/";
    private static readonly XNamespace S3Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    public async Task<NcbiArticleResponse> GetArticleAsync(string pmCid, CancellationToken cancellationToken = default)
    {
        // OAI-PMH requires: oai:pubmedcentral.nih.gov:<AID>
        // Live NCBI rejects "PMC123" and accepts numeric AID only ("123").
        // Callers may still pass PMC-prefixed, versioned, or filename-like values.
        var oaiArticleId = ToOaiArticleId(pmCid);
        var identifier = Uri.EscapeDataString($"oai:pubmedcentral.nih.gov:{oaiArticleId}");
        var requestUri = $"?verb=GetRecord&identifier={identifier}&metadataPrefix=oai_dc";
        var content = await GetContentWithRetryAsync(requestUri, "get article", cancellationToken).ConfigureAwait(false);
        return xmlParser.ParseArticle(content);
    }

    public async Task<NcbiDownloadResponse> GetLiteratureDownloadLinkAsync(string pmcId, CancellationToken cancellationToken = default)
    {
        var normalizedPmcId = NormalizePmcId(pmcId);
        var latestVersion = await ResolveLatestArticleVersionAsync(normalizedPmcId, cancellationToken).ConfigureAwait(false);
        if (latestVersion is null)
        {
            return new NcbiDownloadResponse
            {
                PmcId = normalizedPmcId,
                ArchiveLink = string.Empty,
                PdfLink = string.Empty
            };
        }

        var versionedId = $"{normalizedPmcId}.{latestVersion.Value.ToString(CultureInfo.InvariantCulture)}";
        var metadataUri = $"{PmcOaOpenDataHttpsBaseUrl}/metadata/{versionedId}.json";
        var metadataJson = await GetContentWithRetryAsync(metadataUri, "get PMC OA metadata", cancellationToken)
            .ConfigureAwait(false);

        using var metadata = JsonDocument.Parse(metadataJson);
        var root = metadata.RootElement;

        return new NcbiDownloadResponse
        {
            PmcId = normalizedPmcId,
            // tar.gz packages were removed with the OA Web Service; expose XML as the full-text object.
            ArchiveLink = ConvertS3UrlToHttps(GetOptionalString(root, "xml_url")),
            PdfLink = ConvertS3UrlToHttps(GetOptionalString(root, "pdf_url"))
        };
    }

    private async Task<int?> ResolveLatestArticleVersionAsync(string normalizedPmcId, CancellationToken cancellationToken)
    {
        var listUri =
            $"{PmcOaOpenDataHttpsBaseUrl}/?list-type=2&prefix={Uri.EscapeDataString(normalizedPmcId + ".")}&delimiter=/";
        var listXml = await GetContentWithRetryAsync(listUri, "list PMC OA article versions", cancellationToken)
            .ConfigureAwait(false);

        var document = XDocument.Parse(listXml);
        var versions = document
            .Descendants(S3Namespace + "Prefix")
            .Select(prefix => prefix.Value)
            .Select(TryParseVersionFromPrefix)
            .Where(version => version.HasValue)
            .Select(version => version!.Value)
            .ToList();

        return versions.Count == 0 ? null : versions.Max();
    }

    private static int? TryParseVersionFromPrefix(string prefix)
    {
        // Expected form: PMC5334499.1/
        var trimmed = prefix.TrimEnd('/');
        var separatorIndex = trimmed.LastIndexOf('.');
        if (separatorIndex < 0 || separatorIndex == trimmed.Length - 1)
        {
            return null;
        }

        var versionText = trimmed[(separatorIndex + 1)..];
        return int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;
    }

    /// <summary>
    /// Canonical BioAnalyzer / OA Open Data form: PMC + digits (e.g. PMC5334499).
    /// </summary>
    private static string NormalizePmcId(string pmcId) => "PMC" + ToOaiArticleId(pmcId);

    /// <summary>
    /// Numeric article id for PMC OAI-PMH GetRecord (e.g. 5334499).
    /// </summary>
    internal static string ToOaiArticleId(string pmcId)
    {
        if (string.IsNullOrWhiteSpace(pmcId))
        {
            throw new ArgumentException("pmcId cannot be null or empty.", nameof(pmcId));
        }

        var trimmed = pmcId.Trim();

        // Drop accidental URL/path fragments or file names (e.g. PMC123.pdf from blob keys).
        var slash = trimmed.LastIndexOfAny(['/', '\\']);
        if (slash >= 0 && slash < trimmed.Length - 1)
        {
            trimmed = trimmed[(slash + 1)..];
        }

        var extDot = trimmed.LastIndexOf('.');
        if (extDot > 0)
        {
            var ext = trimmed[(extDot + 1)..];
            // Strip file extensions and OA version suffixes (PMC5334499.1 → PMC5334499).
            if (ext.Equals("pdf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals("xml", StringComparison.OrdinalIgnoreCase)
                || ext.Equals("nxml", StringComparison.OrdinalIgnoreCase)
                || int.TryParse(ext, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                trimmed = trimmed[..extDot];
            }
        }

        if (trimmed.StartsWith("PMC", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[3..];
        }

        trimmed = trimmed.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("pmcId cannot be null or empty.", nameof(pmcId));
        }

        // OAI AID must be digits; reject leftover junk early.
        if (!trimmed.All(char.IsDigit))
        {
            throw new ArgumentException(
                $"pmcId must resolve to numeric PMC article id, got '{pmcId}'.",
                nameof(pmcId));
        }

        return trimmed;
    }

    private static string GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }

    private static string ConvertS3UrlToHttps(string? s3OrHttpsUrl)
    {
        if (string.IsNullOrWhiteSpace(s3OrHttpsUrl))
        {
            return string.Empty;
        }

        if (s3OrHttpsUrl.StartsWith(PmcOaOpenDataS3Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var objectKeyAndQuery = s3OrHttpsUrl[PmcOaOpenDataS3Prefix.Length..];
            return $"{PmcOaOpenDataHttpsBaseUrl}/{objectKeyAndQuery}";
        }

        if (s3OrHttpsUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return s3OrHttpsUrl;
        }

        return s3OrHttpsUrl;
    }

    private async Task<string> GetContentWithRetryAsync(
        string requestUri,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxTooManyRequestsRetries; attempt++)
        {
            await NcbiRequestCoordinator.WaitForRequestSlotAsync(cancellationToken).ConfigureAwait(false);
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            var responseBody = response.Content == null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return responseBody;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxTooManyRequestsRetries)
            {
                var retryDelay = NcbiRequestCoordinator.GetRetryDelay(response.Headers.RetryAfter, attempt);
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new HttpRequestException(
                $"Failed to {operationDescription}. Status {(int)response.StatusCode} {response.ReasonPhrase}. Response: {responseBody}",
                null,
                response.StatusCode);
        }

        throw new HttpRequestException($"Failed to {operationDescription} due to repeated 429 responses.");
    }
}
