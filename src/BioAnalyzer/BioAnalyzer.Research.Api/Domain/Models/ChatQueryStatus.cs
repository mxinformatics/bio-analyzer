namespace BioAnalyzer.Research.Api.Domain.Models;

/// <summary>
/// Machine-readable chat/graph query outcomes for agent tools.
/// Values are stable API contract strings (not free-form).
/// </summary>
public static class ChatQueryStatus
{
    public const string Answered = "Answered";
    public const string NoResults = "NoResults";
    public const string InsufficientEvidence = "InsufficientEvidence";
    public const string GraphUnavailable = "GraphUnavailable";
}
