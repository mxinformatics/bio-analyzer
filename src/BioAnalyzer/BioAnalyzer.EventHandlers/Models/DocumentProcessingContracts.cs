using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;

namespace BioAnalyzer.EventHandlers.Models;

public class ChunkDocumentRequest
{
    [JsonPropertyName("document_name")]
    public string DocumentName { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string?> Metadata { get; set; } = new();
}

public class ChunkDocumentResponse
{
    [JsonPropertyName("document_name")]
    public string DocumentName { get; set; } = string.Empty;

    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("size_bytes")]
    public int SizeBytes { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("total_chunks")]
    public int TotalChunks { get; set; }

    [JsonPropertyName("doc_type")]
    public string DocType { get; set; } = string.Empty;

    [JsonPropertyName("source_system_doc_id")]
    public string SourceSystemDocId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("original_file_name")]
    public string OriginalFileName { get; set; } = string.Empty;

    [JsonPropertyName("selected_source_type")]
    public string SelectedSourceType { get; set; } = string.Empty;

    [JsonPropertyName("selected_source_name")]
    public string SelectedSourceName { get; set; } = string.Empty;

    [JsonPropertyName("source_fallback_reason")]
    public string SourceFallbackReason { get; set; } = string.Empty;
}

public class EmbedDocumentRequest
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;
}

public class EmbedDocumentResponse
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("document_name")]
    public string DocumentName { get; set; } = string.Empty;

    [JsonPropertyName("source_system_doc_id")]
    public string SourceSystemDocId { get; set; } = string.Empty;

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("total_chunks")]
    public int TotalChunks { get; set; }

    [JsonPropertyName("doc_type")]
    public string DocType { get; set; } = string.Empty;

    [JsonPropertyName("original_file_name")]
    public string OriginalFileName { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class BuildGraphRequest
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;
}

public class EnrichGraphRequest
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("pmc_id")]
    public string PmcId { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public List<string> Authors { get; set; } = [];

    [JsonPropertyName("published_date")]
    public string PublishedDate { get; set; } = string.Empty;
    [JsonPropertyName("publication_types")]
    public List<string> PublicationTypes { get; set; } = [];

    [JsonPropertyName("topics")]
    public List<string> Topics { get; set; } = [];
}

public class EnrichGraphResponse
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("nodes_created")]
    public int NodesCreated { get; set; }

    [JsonPropertyName("relationships_created")]
    public int RelationshipsCreated { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class BuildGraphResponse
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("doc_type")]
    public string DocType { get; set; } = string.Empty;

    [JsonPropertyName("total_chunks")]
    public int TotalChunks { get; set; }

    [JsonPropertyName("nodes_created")]
    public int NodesCreated { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class DocumentGraphReady
{
    public string DocumentId { get; set; } = string.Empty;
    public string PmcId { get; set; } = string.Empty;
    public string Doi { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int NodesCreated { get; set; }
    public DateTimeOffset CompletedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class DocumentProcessingFailed
{
    public string DocumentId { get; set; } = string.Empty;
    public string PmcId { get; set; } = string.Empty;
    public string Doi { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int DeliveryCount { get; set; }
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class DocumentProcessingOutputs
{
    [ServiceBusOutput("%DocumentGraphReadyTopic%", Connection = "BioAnalyzerServiceBusSend")]
    public DocumentGraphReady? GraphReady { get; set; }

    [ServiceBusOutput("%DocumentProcessingFailedTopic%", Connection = "BioAnalyzerServiceBusSend")]
    public DocumentProcessingFailed? ProcessingFailed { get; set; }
}

public class DocumentProcessingStatus : ITableEntity
{
    public string PartitionKey { get; set; } = "DocumentProcessingStatus";
    public string RowKey { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string PmcId { get; set; } = string.Empty;
    public string Doi { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
