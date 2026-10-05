namespace BioAnalyzer.Research.Api.Domain.Models;

public static class LiteratureIngestJobStatus
{
    public const string Accepted = "Accepted";
    public const string Downloading = "Downloading";
    public const string Processing = "Processing";
    public const string GraphReady = "GraphReady";
    public const string Failed = "Failed";
    public const string Partial = "Partial";
}

public class LiteratureIngestRequest
{
    public IList<LiteratureIngestItem> Items { get; set; } = [];

    public string? CorrelationId { get; set; }

    public string? RequestedBy { get; set; }

    public string? QueryPreview { get; set; }

    /// <summary>Client-supplied idempotency key (also accepted via Idempotency-Key header).</summary>
    public string? IdempotencyKey { get; set; }
}

public class LiteratureIngestItem
{
    public string PmcId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Doi { get; set; } = string.Empty;

    public string? DownloadLink { get; set; }

    public string? XmlLink { get; set; }
}

public class LiteratureIngestResponse
{
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = LiteratureIngestJobStatus.Accepted;

    public string? CorrelationId { get; set; }

    public IList<LiteratureIngestEnqueueResult> Enqueued { get; set; } = [];

    public IList<LiteratureIngestEnqueueResult> Skipped { get; set; } = [];

    /// <summary>True when this response was served from a prior idempotent request.</summary>
    public bool Replayed { get; set; }
}

public class LiteratureIngestEnqueueResult
{
    public string PmcId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? DownloadLink { get; set; }
}

public class LiteratureIngestJobStatusResult
{
    public string JobId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string? RequestedBy { get; set; }

    public string? QueryPreview { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public IList<LiteratureIngestItemStatus> Items { get; set; } = [];
}

public class LiteratureIngestItemStatus
{
    public string PmcId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Stage { get; set; }

    public string? ErrorMessage { get; set; }

    public string? DocumentId { get; set; }
}
