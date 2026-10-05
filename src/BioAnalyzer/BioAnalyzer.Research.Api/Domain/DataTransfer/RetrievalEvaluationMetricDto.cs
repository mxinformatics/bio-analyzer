using Azure;
using Azure.Data.Tables;

namespace BioAnalyzer.Research.Api.Domain.DataTransfer;

public class RetrievalEvaluationMetricDto : ITableEntity
{
    public string QueryHash { get; set; } = string.Empty;
    public string QueryPreview { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public int SelectedResults { get; set; }
    public double MinScore { get; set; }
    public double AvgScore { get; set; }
    public double MaxScore { get; set; }
    public double EvidenceThreshold { get; set; }
    public int TopK { get; set; }
    public int MinEvidenceCount { get; set; }
    public int MaxEvidenceCount { get; set; }
    public int RetrievalDurationMs { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = "GraphRetrievalEvaluation";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
