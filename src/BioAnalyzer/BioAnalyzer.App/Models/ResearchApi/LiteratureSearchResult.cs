namespace BioAnalyzer.App.Models.ResearchApi;

public class LiteratureSearchResult
{
    public int Count { get; set; }
    public int RetMax { get; set; }
    public int RetStart { get; set; }
    public ICollection<string> ReferenceIds { get; set; } = new List<string>();
}