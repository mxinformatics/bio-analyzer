using System.Net.Http.Json;
using System.Text.Json;
using BioAnalyzer.EventHandlers.Models;
using Microsoft.Extensions.Logging;

namespace BioAnalyzer.EventHandlers.Domain.Clients;

public class ResearchApiClient(HttpClient httpClient, ILogger<ResearchApiClient> logger) : IResearchApiClient
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ArticleMetadata> GetArticleMetadataAsync(string pmcId, CancellationToken cancellationToken = default)
    {
        var requestUri = $"/Literature/metadata?pmcId={Uri.EscapeDataString(pmcId)}";
        logger.LogInformation("Fetching article metadata for PMC ID: {PmcId}", pmcId);

        using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Failed to fetch article metadata for PMC ID {pmcId}. Status {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        var metadata = await response.Content
            .ReadFromJsonAsync<ArticleMetadata>(_jsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return metadata ?? new ArticleMetadata { PmcId = pmcId };
    }
}
