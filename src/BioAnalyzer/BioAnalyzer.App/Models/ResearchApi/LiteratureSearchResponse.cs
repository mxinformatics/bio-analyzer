namespace BioAnalyzer.App.Models.ResearchApi;

public class LiteratureSearchResponse
{
    public int Count { get; set; }
    public int RetMax { get; set; }
    public int RetStart { get; set; }
    public ICollection<string> IdList { get; set; } = new List<string>();
}