namespace BioAnalyzer.Research.Api.Domain.Models;

public class EntrezSummaryResult
{
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    
    public string PmcId { get; set; } = string.Empty;
    
    public string Doi { get; set; } = string.Empty;
    public void SetPmcId(string pmcId)
    {
       PmcId = pmcId.Replace("PMC", "").Replace("pmc", "");
       
    }
}