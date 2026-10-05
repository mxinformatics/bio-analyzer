namespace BioAnalyzer.App.Models.ResearchApi;

public class LiteratureSummaryResult
{
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    
    public string PmcId { get; set; } = string.Empty;
    
    public string Doi { get; set; } = string.Empty;
}