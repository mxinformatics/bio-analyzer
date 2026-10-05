namespace BioAnalyzer.Research.Api.Infrastructure;

public class ResearchApiConfiguration
{
    public string EntrezBaseUrl { get; set; } = string.Empty;
    
    public string NcbiBaseUrl { get; set; } = string.Empty;
    
    public string EntrezTool { get; set; } = "BioAnalyzer";
    
    public string EntrezEmail { get; set; } = string.Empty;
    
    public string EntrezApiKey { get; set; } = string.Empty;
    
    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(EntrezBaseUrl))
        {
            throw new ArgumentException("EntrezBaseUrl must be provided in the configuration.");
        }
        
        if (!Uri.TryCreate(EntrezBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("EntrezBaseUrl must be a valid absolute URL.");
        }
        
        if (string.IsNullOrWhiteSpace(NcbiBaseUrl))
        {
            throw new ArgumentException("NcbiBaseUrl must be provided in the configuration.");
        }
        
        if (!Uri.TryCreate(NcbiBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("NcbiBaseUrl must be a valid absolute URL.");
        }
    }
}