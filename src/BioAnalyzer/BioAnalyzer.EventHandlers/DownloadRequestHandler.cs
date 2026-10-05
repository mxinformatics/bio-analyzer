using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Azure.Messaging.ServiceBus;
using BioAnalyzer.AzureStorage.Contracts.Models;
using BioAnalyzer.EventHandlers.Domain.Clients;
using BioAnalyzer.EventHandlers.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BioAnalyzer.EventHandlers;

public class DownloadRequestHandler
{
    private const string PmcOpenDataBaseUrl = "https://pmc-oa-opendata.s3.amazonaws.com";
    private const string MetadataPrefix = "metadata";
    private readonly ILogger<DownloadRequestHandler> _logger;
    private readonly IStorageClient _storageClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIngestJobStatusClient _ingestJobStatusClient;
    public DownloadRequestHandler(
        ILogger<DownloadRequestHandler> logger,
        IStorageClient storageClient,
        IHttpClientFactory httpClientFactory,
        IIngestJobStatusClient ingestJobStatusClient)
    {
        _logger = logger;
        _storageClient = storageClient;
        _httpClientFactory = httpClientFactory;
        _ingestJobStatusClient = ingestJobStatusClient;
    }

    /// <summary>
    /// Downloads OA content and, on success only, emits <see cref="DownloadedLiterature"/> for processing.
    /// Returning null suppresses the Service Bus output binding so failed downloads never start chunk/embed/graph.
    /// </summary>
    [Function(nameof(DownloadRequestHandler))]
    [ServiceBusOutput("%DocumentDownloadedTopic%", Connection = "BioAnalyzerServiceBusSend")]
    public async Task<DownloadedLiterature?> Run(
        [ServiceBusTrigger("%DownloadDocumentQueue%", Connection = "BioAnalyzerServiceBusListen")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);

        var downloadRequest = message.Body.ToObjectFromJson<DownloadRequest>();
        if (downloadRequest == null)
        {
            _logger.LogError("Download request payload is null for message ID: {id}", message.MessageId);
            await messageActions
                .DeadLetterMessageAsync(
                    message,
                    deadLetterReason: "InvalidPayload",
                    deadLetterErrorDescription: "Download request payload is null or could not be deserialized.")
                .ConfigureAwait(false);
            // No processing output for corrupt payloads.
            return null;
        }

        if (!string.IsNullOrWhiteSpace(downloadRequest.JobId))
        {
            await _ingestJobStatusClient
                .MarkItemProgressAsync(
                    downloadRequest.JobId,
                    downloadRequest.PmcId,
                    itemStatus: "Downloading",
                    stage: "Download")
                .ConfigureAwait(false);
        }

        var resolvedDownloadLink = downloadRequest.DownloadLink;
        var resolvedXmlLink = string.Empty;
        var resolvedFileName = $"{downloadRequest.PmcId}.pdf";
        var downloadSucceeded = false;
        string? failureError = null;
        try
        {
            var resolvedLinks = await DownloadFile(downloadRequest).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(resolvedLinks?.SelectedDownloadLink))
            {
                resolvedDownloadLink = resolvedLinks.SelectedDownloadLink;
                downloadSucceeded = true;
            }
            else
            {
                failureError = "Unable to resolve or download PMC Open Data content.";
            }

            if (!string.IsNullOrWhiteSpace(resolvedLinks?.XmlLink))
            {
                resolvedXmlLink = resolvedLinks.XmlLink;
            }

            if (!string.IsNullOrWhiteSpace(resolvedLinks?.FileName))
            {
                resolvedFileName = resolvedLinks.FileName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while downloading file for PMCID: {PmcId}", downloadRequest.PmcId);
            failureError = ex.Message;
        }

        if (!downloadSucceeded)
        {
            var errorMessage = string.IsNullOrWhiteSpace(failureError)
                ? "Unable to resolve or download PMC Open Data content."
                : failureError;

            if (!string.IsNullOrWhiteSpace(downloadRequest.JobId))
            {
                await _ingestJobStatusClient
                    .MarkItemProgressAsync(
                        downloadRequest.JobId,
                        downloadRequest.PmcId,
                        itemStatus: "Failed",
                        stage: "Download",
                        errorMessage: errorMessage)
                    .ConfigureAwait(false);
            }

            _logger.LogWarning(
                "Download failed for PMCID: {PmcId}. Completing trigger without emitting DownloadedLiterature. Error: {Error}",
                downloadRequest.PmcId,
                errorMessage);

            // Permanent resolve/download failures: complete without output (no infinite retry, no processing).
            await messageActions.CompleteMessageAsync(message).ConfigureAwait(false);
            return null;
        }

        await messageActions.CompleteMessageAsync(message).ConfigureAwait(false);

        return new DownloadedLiterature
        {
            FileName = resolvedFileName,
            Title = downloadRequest.Title,
            DownloadLink = resolvedDownloadLink,
            XmlDownloadLink = resolvedXmlLink,
            Doi = downloadRequest.Doi,
            PmcId = downloadRequest.PmcId,
            JobId = downloadRequest.JobId
        };
    }

    private async Task<PmcOpenDataResolvedLinks?> DownloadFile(DownloadRequest downloadRequest)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var resolvedDownloadLinks = await ResolvePmcOpenDataLinks(downloadRequest.PmcId, httpClient).ConfigureAwait(false);
        if (resolvedDownloadLinks == null)
        {
            _logger.LogError("Unable to resolve PMC Open Data links for PMCID: {PmcId}", downloadRequest.PmcId);
            return null;
        }

        var selectedDownload = SelectDownloadTarget(downloadRequest.PmcId, resolvedDownloadLinks);
        if (selectedDownload == null)
        {
            _logger.LogError("Unable to resolve a PMC Open Data XML/PDF URL for PMCID: {PmcId}", downloadRequest.PmcId);
            return null;
        }

        var response = await httpClient.GetAsync(selectedDownload.DownloadUri).ConfigureAwait(false);
        
        if (response.IsSuccessStatusCode)
        {
            var fileContent = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            await UploadDocument(selectedDownload.FileName, selectedDownload.ContentType, fileContent).ConfigureAwait(false);
            return new PmcOpenDataResolvedLinks(
                selectedDownload.DownloadUri.ToString(),
                selectedDownload.FileName,
                resolvedDownloadLinks.XmlUri?.ToString());
        }

        _logger.LogError("Failed to download file from {DownloadUri}. Status code: {StatusCode}",
            selectedDownload.DownloadUri, response.StatusCode);
        return null;
    }

    private async Task UploadDocument(string fileName, DocumentContentType contentType, byte[] fileContent)
    {
        await _storageClient.UploadDocumentAsync(fileName, contentType, fileContent).ConfigureAwait(false);
    }

    private async Task<PmcOpenDataDownloadLinks?> ResolvePmcOpenDataLinks(string pmcId, HttpClient httpClient)
    {
        var normalizedPmcId = NormalizePmcId(pmcId);
        var metadataPrefix = $"{MetadataPrefix}/{normalizedPmcId}.";
        var listUrl = $"{PmcOpenDataBaseUrl}/?list-type=2&prefix={Uri.EscapeDataString(metadataPrefix)}&max-keys=1000";

        var listResponse = await httpClient.GetAsync(listUrl).ConfigureAwait(false);
        if (!listResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("PMC Open Data metadata listing failed for {PmcId}. Status code: {StatusCode}",
                normalizedPmcId, listResponse.StatusCode);
            return null;
        }

        var listXml = await listResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        var metadataKey = GetLatestMetadataKey(listXml, normalizedPmcId);
        if (string.IsNullOrWhiteSpace(metadataKey))
        {
            _logger.LogWarning("No PMC Open Data metadata object found for {PmcId}", normalizedPmcId);
            return null;
        }

        var metadataUri = new Uri($"{PmcOpenDataBaseUrl}/{metadataKey}");
        var metadataResponse = await httpClient.GetAsync(metadataUri).ConfigureAwait(false);
        if (!metadataResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("PMC Open Data metadata fetch failed for {MetadataUri}. Status code: {StatusCode}",
                metadataUri, metadataResponse.StatusCode);
            return null;
        }

        var metadataJson = await metadataResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        var metadata = JsonSerializer.Deserialize<PmcOpenDataMetadata>(metadataJson);
        Uri? pdfUri = null;
        if (!string.IsNullOrWhiteSpace(metadata?.PdfUrl))
        {
            pdfUri = ConvertToHttpsUri(metadata.PdfUrl);
        }

        var xmlUri = ResolveXmlUri(metadata);
        if (pdfUri == null && xmlUri == null)
        {
            _logger.LogWarning("PMC Open Data metadata has no resolvable XML or PDF URL for {PmcId}", normalizedPmcId);
            return null;
        }

        return new PmcOpenDataDownloadLinks(pdfUri, xmlUri);
    }


    private static string NormalizePmcId(string pmcId)
    {
        if (string.IsNullOrWhiteSpace(pmcId))
        {
            return string.Empty;
        }

        var trimmed = pmcId.Trim();
        return trimmed.StartsWith("PMC", StringComparison.OrdinalIgnoreCase)
            ? trimmed.ToUpperInvariant()
            : $"PMC{trimmed}";
    }

    private static string? GetLatestMetadataKey(string listXml, string normalizedPmcId)
    {
        var document = XDocument.Parse(listXml);
        var versionedKeys = document.Descendants()
            .Where(element => element.Name.LocalName == "Key")
            .Select(element => element.Value)
            .Select(key => new { Key = key, Version = TryParseMetadataVersion(key, normalizedPmcId) })
            .Where(result => result.Version.HasValue)
            .OrderByDescending(result => result.Version!.Value)
            .ToList();

        return versionedKeys.FirstOrDefault()?.Key;
    }

    private static int? TryParseMetadataVersion(string key, string normalizedPmcId)
    {
        var keyPrefix = $"{MetadataPrefix}/{normalizedPmcId}.";
        if (!key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase)
            || !key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var versionText = key.Substring(keyPrefix.Length, key.Length - keyPrefix.Length - ".json".Length);
        return int.TryParse(versionText, out var version) ? version : null;
    }

    private static Uri? ConvertToHttpsUri(string downloadUrl)
    {
        if (downloadUrl.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var s3Path = downloadUrl["s3://".Length..];
            var firstSlashIndex = s3Path.IndexOf('/');
            if (firstSlashIndex <= 0)
            {
                return null;
            }

            var bucket = s3Path[..firstSlashIndex];
            var key = s3Path[(firstSlashIndex + 1)..];
            downloadUrl = $"https://{bucket}.s3.amazonaws.com/{key}";
        }

        return Uri.TryCreate(downloadUrl, UriKind.Absolute, out var resolvedUri) ? resolvedUri : null;
    }

    private static Uri? ResolveXmlUri(PmcOpenDataMetadata? metadata)
    {
        if (metadata == null)
        {
            return null;
        }

        var directCandidates = new[]
        {
            metadata.XmlUrl,
            metadata.FullTextXmlUrl,
            metadata.NxmlUrl
        };

        foreach (var candidate in directCandidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                var converted = ConvertToHttpsUri(candidate);
                if (converted != null)
                {
                    return converted;
                }
            }
        }

        if (metadata.AdditionalData == null || metadata.AdditionalData.Count == 0)
        {
            return null;
        }

        foreach (var entry in metadata.AdditionalData)
        {
            if (entry.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var candidate = entry.Value.GetString();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!entry.Key.Contains("xml", StringComparison.OrdinalIgnoreCase)
                && !candidate.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                && !candidate.EndsWith(".nxml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var converted = ConvertToHttpsUri(candidate);
            if (converted != null)
            {
                return converted;
            }
        }

        return null;
    }

    private static PmcOpenDataDownloadTarget? SelectDownloadTarget(string pmcId, PmcOpenDataDownloadLinks links)
    {
        if (links.XmlUri != null)
        {
            var xmlFileName = ResolveDownloadFileName(pmcId, links.XmlUri, ".xml");
            return new PmcOpenDataDownloadTarget(links.XmlUri, xmlFileName, DocumentContentType.Xml);
        }

        if (links.PdfUri != null)
        {
            var pdfFileName = ResolveDownloadFileName(pmcId, links.PdfUri, ".pdf");
            return new PmcOpenDataDownloadTarget(links.PdfUri, pdfFileName, DocumentContentType.Pdf);
        }

        return null;
    }

    private static string ResolveDownloadFileName(string pmcId, Uri downloadUri, string defaultExtension)
    {
        var extension = Path.GetExtension(downloadUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = defaultExtension;
        }

        return $"{pmcId}{extension.ToLowerInvariant()}";
    }

    private sealed class PmcOpenDataMetadata
    {
        [JsonPropertyName("pdf_url")]
        public string? PdfUrl { get; init; }

        [JsonPropertyName("xml_url")]
        public string? XmlUrl { get; init; }

        [JsonPropertyName("fulltext_xml_url")]
        public string? FullTextXmlUrl { get; init; }

        [JsonPropertyName("nxml_url")]
        public string? NxmlUrl { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; init; }
    }

    private sealed record PmcOpenDataDownloadLinks(Uri? PdfUri, Uri? XmlUri);
    private sealed record PmcOpenDataDownloadTarget(Uri DownloadUri, string FileName, DocumentContentType ContentType);
    private sealed record PmcOpenDataResolvedLinks(string SelectedDownloadLink, string FileName, string? XmlLink);
}
