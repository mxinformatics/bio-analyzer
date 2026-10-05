namespace BioAnalyzer.Research.Api.Domain.Models;

public class EntrezSummaryResponse
{
    public EntrezSummaryResponse()
    {
    }

    public EntrezSummaryResponse(IList<EntrezSummaryResult> results)
    {
        Results = results;
    }

    public IList<EntrezSummaryResult> Results { get; set; } = new List<EntrezSummaryResult>();
}