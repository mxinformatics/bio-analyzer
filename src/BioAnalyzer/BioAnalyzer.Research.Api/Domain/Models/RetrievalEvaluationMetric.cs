namespace BioAnalyzer.Research.Api.Domain.Models;

public class RetrievalEvaluationMetric
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
    public DateTimeOffset EvaluatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
