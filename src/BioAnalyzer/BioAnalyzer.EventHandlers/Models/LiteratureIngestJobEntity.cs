using Azure;
using Azure.Data.Tables;

namespace BioAnalyzer.EventHandlers.Models;

/// <summary>
/// Shared Azure Table schema with Research.Api LiteratureIngestJobs.
/// </summary>
public class LiteratureIngestJobEntity : ITableEntity
{
    public const string DefaultPartitionKey = "LiteratureIngestJob";

    public string PartitionKey { get; set; } = DefaultPartitionKey;

    public string RowKey { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = string.Empty;

    public string QueryPreview { get; set; } = string.Empty;

    public string ItemsJson { get; set; } = "[]";

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
