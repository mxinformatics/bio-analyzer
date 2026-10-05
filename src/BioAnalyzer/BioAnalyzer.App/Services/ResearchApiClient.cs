using BioAnalyzer.App.Contracts.Clients;
using BioAnalyzer.App.Models;
using BioAnalyzer.App.Models.ResearchApi;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;


namespace BioAnalyzer.App.Services;

public class ResearchApiClient(HttpClient httpClient, ILogger<ResearchApiClient> logger) : IResearchApiClient
{
    public async Task<LiteratureSearchResult> GetLiteratureReferences(string searchTerm, int index, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm, nameof(searchTerm));
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        
        try
        {
            var requestUri = $"/literature?query={Uri.EscapeDataString(searchTerm)}&startIndex={index}";
            logger.LogDebug("Calling Research API: {RequestUri}", requestUri);
            
            var result = await httpClient.GetFromJsonAsync<LiteratureSearchResponse>(requestUri, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                logger.LogError("Research API returned null response for search term: {SearchTerm}", searchTerm);
                throw new InvalidOperationException("Response content is null.");
            }

            logger.LogDebug("Research API returned {Count} results for search term: {SearchTerm}", result.Count, searchTerm);
            
            return new LiteratureSearchResult
            {
                Count = result.Count,
                ReferenceIds = result.IdList,
                RetMax = result.RetMax,
                RetStart = result.RetStart
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to get literature references from Research API for search term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task<LiteratureDownload> UploadManualReference(
        string title,
        string pmcId,
        string doi,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
        ArgumentNullException.ThrowIfNull(content, nameof(content));

        using var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(new StringContent(title ?? string.Empty), "title");
        multipartContent.Add(new StringContent(pmcId ?? string.Empty), "pmcId");
        multipartContent.Add(new StringContent(doi ?? string.Empty), "doi");

        var fileContent = new StreamContent(content);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }
        multipartContent.Add(fileContent, "file", fileName);

        using var response = await httpClient
            .PostAsync("/literature/uploads/manual", multipartContent, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<LiteratureDownload>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (result == null)
        {
            throw new InvalidOperationException("Manual upload response content is null.");
        }

        return result;
    }

    public async Task<IList<LiteratureSummaryResult>> GetLiteratureSummary(IList<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids, nameof(ids));
        
        if (ids.Count == 0)
        {
            throw new ArgumentException("ids list cannot be empty", nameof(ids));
        }
        
        try
        {
            var requestUri = $"/literature/summary?ids={string.Join(",", ids)}";
            logger.LogDebug("Calling Research API to get summaries for {IdCount} references", ids.Count);
            
            var result = await httpClient.GetFromJsonAsync<IList<LiteratureSummaryResult>>(requestUri, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                logger.LogError("Research API returned null response for literature summaries");
                throw new InvalidOperationException("Response content is null.");
            }

            logger.LogDebug("Successfully retrieved {SummaryCount} literature summaries", result.Count);
            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to get literature summaries from Research API");
            throw;
        }
    }

    public async Task<LiteratureAbstract> GetLiteratureAbstract(string pmcId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pmcId, nameof(pmcId));
        
        try
        {
            var requestUri = $"/literature/abstract?pmcId={pmcId}";
            logger.LogDebug("Calling Research API to get abstract for PmcId: {PmcId}", pmcId);
            
            var result = await httpClient.GetFromJsonAsync<LiteratureAbstract>(requestUri, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                logger.LogError("Research API returned null response for abstract with PmcId: {PmcId}", pmcId);
                throw new InvalidOperationException("Response content is null.");
            }
            
            logger.LogDebug("Successfully retrieved abstract for PmcId: {PmcId}", pmcId);
            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to get literature abstract from Research API for PmcId: {PmcId}", pmcId);
            throw;
        }
    }

    public async Task<LiteratureDownloadLinkResponse> DownloadReference(LiteratureReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference, nameof(reference));
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.PmcId, nameof(reference.PmcId));
        
        try
        {
            var requestUri = $"/literature/download?pmcId={reference.PmcId}";
            logger.LogDebug("Calling Research API to get download link for PmcId: {PmcId}", reference.PmcId);
            
            var result = await httpClient.GetFromJsonAsync<LiteratureDownloadLinkResponse>(requestUri, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                logger.LogError("Research API returned null response for download link with PmcId: {PmcId}", reference.PmcId);
                throw new InvalidOperationException("Response content is null.");
            }
            
            logger.LogDebug("Successfully retrieved download link for PmcId: {PmcId}", reference.PmcId);
            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to get download link from Research API for PmcId: {PmcId}", reference.PmcId);
            throw;
        }
    }

    public async Task<LiteratureDownloadsResponse> GetDownloads(CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri = "/literature/downloads/view";
            logger.LogDebug("Calling Research API to get downloads list");
            
            var result = await httpClient.GetFromJsonAsync<LiteratureDownloadsResponse>(requestUri, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                logger.LogError("Research API returned null response for downloads list");
                throw new InvalidOperationException("Response content is null.");
            }

            logger.LogDebug("Successfully retrieved downloads list with {DownloadCount} items", result.Downloads.Count);
            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to get downloads list from Research API");
            throw;
        }
    }

    public async Task<byte[]> DownloadFile(string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
        
        try
        {
            var requestUri = $"/literature/downloads/{fileName}";
            logger.LogDebug("Calling Research API to download file: {FileName}", fileName);
            
            var result = await httpClient.GetByteArrayAsync(requestUri, cancellationToken).ConfigureAwait(false);
            
            logger.LogDebug("Successfully downloaded file: {FileName}, Size: {FileSize} bytes", fileName, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download file from Research API: {FileName}", fileName);
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamChatQueryAsync(string query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        var requestUri = $"/chat/stream?query={Uri.EscapeDataString(query)}";
        logger.LogDebug("Calling Research API chat stream: {RequestUri}", requestUri);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[5..].Trim();
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                yield break;
            }

            string? token;
            try
            {
                token = JsonSerializer.Deserialize<string>(payload);
            }
            catch (JsonException)
            {
                token = payload;
            }

            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }
        }
    }
}