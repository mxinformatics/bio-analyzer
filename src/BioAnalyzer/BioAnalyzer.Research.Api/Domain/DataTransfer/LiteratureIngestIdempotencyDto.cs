using Azure;
using Azure.Data.Tables;

namespace BioAnalyzer.Research.Api.Domain.DataTransfer;

public class LiteratureIngestIdempotencyDto : ITableEntity
{
    public const string DefaultPartitionKey = "LiteratureIngestIdempotency";

    public string PartitionKey { get; set; } = DefaultPartitionKey;

    public string RowKey { get; set; } = string.Empty;

    public string JobId { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hex fingerprint of the logical request body (sorted PMCIDs + requester + query preview).
    /// Same key + different fingerprint => conflict (409).
    /// </summary>
    public string RequestFingerprint { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
