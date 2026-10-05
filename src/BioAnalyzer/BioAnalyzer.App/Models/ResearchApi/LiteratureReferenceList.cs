namespace BioAnalyzer.App.Models.ResearchApi;

public class LiteratureReferenceList
{
    public int Count { get; set; }
    public int RetMax { get; set; }
    public int RetStart { get; set; }
    public ICollection<LiteratureReference> References { get; set; } = new List<LiteratureReference>();
}