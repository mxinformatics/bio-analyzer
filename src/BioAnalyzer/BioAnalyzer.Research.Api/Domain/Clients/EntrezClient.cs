using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Domain.Parsers;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace BioAnalyzer.Research.Api.Domain.Clients;

/// <summary>
/// Implementation of the Entrez client for searching the Entrez database.
/// </summary>
public class EntrezClient(HttpClient httpClient, IEntrezXmlParser xmlParser, IOptions<ResearchApiConfiguration> configuration)
    : IEntrezClient
{
    private const int MaxTooManyRequestsRetries = 2;
    private readonly HttpClient _httpClient = httpClient;
    private readonly IEntrezXmlParser _xmlParser = xmlParser;
    private readonly ResearchApiConfiguration _configuration = configuration.Value;

public async Task<EntrezSearchResult> LiteratureSearchAsync(string query, int startIndex, int retMax = 20, CancellationToken cancellationToken = default)
    {
        var safeRetMax = retMax <= 0 ? 20 : retMax;
        var requestUri = BuildEntrezRequestUri(
            $"esearch.fcgi?db=pubmed&term={Uri.EscapeDataString(query)}&retmode=json&retstart={startIndex}&retmax={safeRetMax}");
        var content = await GetContentWithRetryAsync(requestUri, "search literature", cancellationToken).ConfigureAwait(false);
        var parsedContent = JsonSerializer.Deserialize<EntrezSearchResponse>(content);
        if (parsedContent?.ESearchResult == null)
        {
            throw new InvalidOperationException("Response content is null.");
        }

        return parsedContent.ESearchResult;
    }

    public async Task<EntrezSummaryResponse> LiteratureSummaryAsync(IList<string> uids, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildEntrezRequestUri(
            $"esummary.fcgi?db=pubmed&id={string.Join(",", uids)}&retmode=xml");
        var contentXml = await GetContentWithRetryAsync(requestUri, "get literature summary", cancellationToken)
            .ConfigureAwait(false);
        return _xmlParser.ParseSummary(contentXml);
    }

    private async Task<string> GetContentWithRetryAsync(
        string requestUri,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxTooManyRequestsRetries; attempt++)
        {
            await NcbiRequestCoordinator.WaitForRequestSlotAsync(cancellationToken).ConfigureAwait(false);
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
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

    private string BuildEntrezRequestUri(string requestUri)
    {
        var additionalQueryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(_configuration.EntrezTool))
        {
            additionalQueryParts.Add($"tool={Uri.EscapeDataString(_configuration.EntrezTool)}");
        }

        if (!string.IsNullOrWhiteSpace(_configuration.EntrezEmail))
        {
            additionalQueryParts.Add($"email={Uri.EscapeDataString(_configuration.EntrezEmail)}");
        }

        if (!string.IsNullOrWhiteSpace(_configuration.EntrezApiKey))
        {
            additionalQueryParts.Add($"api_key={Uri.EscapeDataString(_configuration.EntrezApiKey)}");
        }

        if (!additionalQueryParts.Any())
        {
            return requestUri;
        }

        var separator = requestUri.Contains('?') ? "&" : "?";
        return $"{requestUri}{separator}{string.Join("&", additionalQueryParts)}";
    }
}
