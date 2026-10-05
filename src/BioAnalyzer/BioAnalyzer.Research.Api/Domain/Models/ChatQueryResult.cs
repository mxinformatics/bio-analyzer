namespace BioAnalyzer.Research.Api.Domain.Models;

public class ChatQueryRequest
{
    public string Query { get; set; } = string.Empty;
}

public class ChatQueryResult
{
    /// <summary>
    /// One of <see cref="ChatQueryStatus"/> values.
    /// </summary>
    public string Status { get; set; } = ChatQueryStatus.InsufficientEvidence;

    public string Query { get; set; } = string.Empty;

    public string QueryHash { get; set; } = string.Empty;

    /// <summary>
    /// Model answer when status is Answered; otherwise often the standard insufficient-evidence message.
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    public ChatRetrievalSummary Retrieval { get; set; } = new();

    public IList<ChatEvidenceSource> Sources { get; set; } = [];

    /// <summary>
    /// Underlying retrieval metric status (Success, ThresholdBypass, NoResults, etc.).
    /// </summary>
    public string RetrievalMetricStatus { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public class ChatRetrievalSummary
{
    public int TotalResults { get; set; }

    public int SelectedResults { get; set; }

    public double MinScore { get; set; }

    public double AvgScore { get; set; }

    public double MaxScore { get; set; }

    public double Threshold { get; set; }

    public int TopK { get; set; }

    public int MinEvidenceCount { get; set; }

    public int MaxEvidenceCount { get; set; }

    public int RetrievalDurationMs { get; set; }
}

public class ChatEvidenceSource
{
    public int Index { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string SourceSystemDocId { get; set; } = string.Empty;

    public string PmcId { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public int ChunkIndex { get; set; }

    public double Score { get; set; }

    public string Evidence { get; set; } = string.Empty;
}
