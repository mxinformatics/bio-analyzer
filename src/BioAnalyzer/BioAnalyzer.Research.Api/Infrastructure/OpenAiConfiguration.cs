namespace BioAnalyzer.Research.Api.Infrastructure;

public class OpenAiConfiguration
{
    public string Endpoint { get; set; } = string.Empty;
    public string GptName { get; set; } = string.Empty;
    public string GraphApiUrl { get; set; } = string.Empty;
    public string GraphApiKey { get; set; } = string.Empty;
    public int GraphTopK { get; set; } = 5;
    public int GraphQueryTimeoutSeconds { get; set; } = 30;
    public int GraphMaxRetryAttempts { get; set; } = 3;
    public int GraphInitialBackoffMilliseconds { get; set; } = 250;
    public int GraphCircuitBreakerFailureThreshold { get; set; } = 5;
    public int GraphCircuitBreakerBreakSeconds { get; set; } = 60;
    public double GraphEvidenceMinScore { get; set; } = 0.25;
    public int GraphMinEvidenceCount { get; set; } = 1;
    public int GraphMaxEvidenceCount { get; set; } = 8;
    public bool GraphRequireCitationsInResponse { get; set; } = true;
    
    
    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("OpenAI Endpoint is required");
        }
        if (string.IsNullOrWhiteSpace(GptName))
        {
            throw new InvalidOperationException("OpenAI GPT Name is required");
        }
        if (string.IsNullOrWhiteSpace(GraphApiUrl))
        {
            throw new InvalidOperationException("Graph API URL is required");
        }
        if (GraphTopK <= 0)
        {
            throw new InvalidOperationException("GraphTopK must be greater than zero");
        }
        if (GraphQueryTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("GraphQueryTimeoutSeconds must be greater than zero");
        }
        if (GraphMaxRetryAttempts <= 0)
        {
            throw new InvalidOperationException("GraphMaxRetryAttempts must be greater than zero");
        }
        if (GraphInitialBackoffMilliseconds <= 0)
        {
            throw new InvalidOperationException("GraphInitialBackoffMilliseconds must be greater than zero");
        }
        if (GraphCircuitBreakerFailureThreshold <= 0)
        {
            throw new InvalidOperationException("GraphCircuitBreakerFailureThreshold must be greater than zero");
        }
        if (GraphCircuitBreakerBreakSeconds <= 0)
        {
            throw new InvalidOperationException("GraphCircuitBreakerBreakSeconds must be greater than zero");
        }
        if (GraphEvidenceMinScore is < 0 or > 1)
        {
            throw new InvalidOperationException("GraphEvidenceMinScore must be between 0 and 1");
        }
        if (GraphMinEvidenceCount <= 0)
        {
            throw new InvalidOperationException("GraphMinEvidenceCount must be greater than zero");
        }
        if (GraphMaxEvidenceCount <= 0)
        {
            throw new InvalidOperationException("GraphMaxEvidenceCount must be greater than zero");
        }
        if (GraphMinEvidenceCount > GraphMaxEvidenceCount)
        {
            throw new InvalidOperationException("GraphMinEvidenceCount cannot be greater than GraphMaxEvidenceCount");
        }
    }
}