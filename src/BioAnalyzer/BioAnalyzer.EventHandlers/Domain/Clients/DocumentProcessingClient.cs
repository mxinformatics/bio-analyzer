using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BioAnalyzer.EventHandlers.Infrastructure;
using BioAnalyzer.EventHandlers.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.EventHandlers.Domain.Clients;

public class DocumentProcessingClient(
    IHttpClientFactory httpClientFactory,
    IOptions<DocumentProcessingConfiguration> processingConfiguration,
    ILogger<DocumentProcessingClient> logger) : IDocumentProcessingClient
{
    public const string ChunkClientName = "document-processing-chunk";
    public const string EmbeddingClientName = "document-processing-embedding";
    public const string GraphClientName = "document-processing-graph";
    public const string GraphEnrichClientName = "document-processing-enrich";

    private readonly DocumentProcessingConfiguration _processingConfiguration = processingConfiguration.Value;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ChunkDocumentResponse> ChunkDocumentAsync(
        DownloadedLiterature downloadedLiterature,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var documentName = string.IsNullOrWhiteSpace(downloadedLiterature.Title)
            ? documentId
            : downloadedLiterature.Title;

        var request = new ChunkDocumentRequest
        {
            DocumentName = documentName,
            FileName = downloadedLiterature.FileName,
            DocumentId = documentId,
            Metadata = new Dictionary<string, string?>
            {
                ["pmc_id"] = downloadedLiterature.PmcId,
                ["doi"] = downloadedLiterature.Doi,
                ["title"] = downloadedLiterature.Title,
                ["download_link"] = downloadedLiterature.DownloadLink
            }
        };

        if (!string.IsNullOrWhiteSpace(downloadedLiterature.XmlDownloadLink))
        {
            request.Metadata["xml_url"] = downloadedLiterature.XmlDownloadLink;
            request.Metadata["jats_xml_url"] = downloadedLiterature.XmlDownloadLink;
        }

        logger.LogInformation("Submitting chunk request for document ID: {DocumentId}", documentId);
        return await PostAsync<ChunkDocumentRequest, ChunkDocumentResponse>(
                ChunkClientName,
                "/chunk",
                request,
                _processingConfiguration.ChunkApiKey,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EmbedDocumentResponse> EmbedDocumentAsync(
        ChunkDocumentResponse chunkResponse,
        CancellationToken cancellationToken = default)
    {
        var request = new EmbedDocumentRequest
        {
            DocumentId = chunkResponse.DocumentId
        };

        logger.LogInformation("Submitting embedding request for document ID: {DocumentId}", request.DocumentId);
        return await PostAsync<EmbedDocumentRequest, EmbedDocumentResponse>(
                EmbeddingClientName,
                "/embed",
                request,
                _processingConfiguration.EmbeddingApiKey,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BuildGraphResponse> BuildGraphAsync(
        EmbedDocumentResponse embedResponse,
        CancellationToken cancellationToken = default)
    {
        var request = new BuildGraphRequest
        {
            DocumentId = embedResponse.DocumentId
        };

        logger.LogInformation("Submitting graph build request for document ID: {DocumentId}", request.DocumentId);
        return await PostAsync<BuildGraphRequest, BuildGraphResponse>(
                GraphClientName,
                "/build-graph",
                request,
                _processingConfiguration.GraphDataApiKey,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EnrichGraphResponse> EnrichGraphAsync(
        string documentId,
        ArticleMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var request = new EnrichGraphRequest
        {
            DocumentId = documentId,
            PmcId = metadata.PmcId,
            Authors = metadata.Authors,
            PublishedDate = metadata.PublishedDate,
            PublicationTypes = metadata.PublicationTypes,
            Topics = []
        };

        logger.LogInformation("Submitting graph enrich request for document ID: {DocumentId}", documentId);
        return await PostAsync<EnrichGraphRequest, EnrichGraphResponse>(
                GraphEnrichClientName,
                "/enrich-graph",
                request,
                _processingConfiguration.GraphDataApiKey,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string clientName,
        string path,
        TRequest payload,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(clientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: _jsonSerializerOptions)
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Request to {path} failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var parsedResponse = await response.Content
            .ReadFromJsonAsync<TResponse>(_jsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (parsedResponse == null)
        {
            throw new InvalidOperationException($"Request to {path} returned an empty response body.");
        }

        return parsedResponse;
    }
}
