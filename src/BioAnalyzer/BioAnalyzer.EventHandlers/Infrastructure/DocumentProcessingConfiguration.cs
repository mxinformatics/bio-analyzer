namespace BioAnalyzer.EventHandlers.Infrastructure;

public class DocumentProcessingConfiguration
{
    public string ChunkApiUrl { get; set; } = string.Empty;
    public string EmbeddingApiUrl { get; set; } = string.Empty;
    public string GraphDataApiUrl { get; set; } = string.Empty;
    public string ResearchApiUrl { get; set; } = string.Empty;
    public string ChunkApiKey { get; set; } = string.Empty;
    public string EmbeddingApiKey { get; set; } = string.Empty;
    public string GraphDataApiKey { get; set; } = string.Empty;
    public string ResearchApiKey { get; set; } = string.Empty;

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(ChunkApiUrl))
        {
            throw new InvalidOperationException("DocumentProcessing ChunkApiUrl is required");
        }

        if (string.IsNullOrWhiteSpace(EmbeddingApiUrl))
        {
            throw new InvalidOperationException("DocumentProcessing EmbeddingApiUrl is required");
        }

        if (string.IsNullOrWhiteSpace(GraphDataApiUrl))
        {
            throw new InvalidOperationException("DocumentProcessing GraphDataApiUrl is required");
        }

        if (string.IsNullOrWhiteSpace(ResearchApiUrl))
        {
            throw new InvalidOperationException("DocumentProcessing ResearchApiUrl is required");
        }
    }
}
