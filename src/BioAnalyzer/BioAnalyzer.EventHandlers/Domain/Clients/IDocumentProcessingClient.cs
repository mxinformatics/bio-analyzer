using BioAnalyzer.EventHandlers.Models;

namespace BioAnalyzer.EventHandlers.Domain.Clients;

public interface IDocumentProcessingClient
{
    Task<ChunkDocumentResponse> ChunkDocumentAsync(
        DownloadedLiterature downloadedLiterature,
        string documentId,
        CancellationToken cancellationToken = default);

    Task<EmbedDocumentResponse> EmbedDocumentAsync(
        ChunkDocumentResponse chunkResponse,
        CancellationToken cancellationToken = default);

    Task<BuildGraphResponse> BuildGraphAsync(
        EmbedDocumentResponse embedResponse,
        CancellationToken cancellationToken = default);

    Task<EnrichGraphResponse> EnrichGraphAsync(
        string documentId,
        ArticleMetadata metadata,
        CancellationToken cancellationToken = default);
}
