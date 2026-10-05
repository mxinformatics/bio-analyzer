using System.Text.Json;
using System.Text.Json.Serialization;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public class GraphQueryRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 5;
}

public class GraphQueryResponse
{
    [JsonPropertyName("results")]
    public List<GraphQueryResultItem> Results { get; set; } = new();
}

public class GraphQueryResultItem
{
    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("document_name")]
    public string DocumentName { get; set; } = string.Empty;

    [JsonPropertyName("source_system_doc_id")]
    public string SourceSystemDocId { get; set; } = string.Empty;

    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement>? Metadata { get; set; }
}