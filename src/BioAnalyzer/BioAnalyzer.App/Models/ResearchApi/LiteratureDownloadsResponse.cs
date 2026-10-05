namespace BioAnalyzer.App.Models.ResearchApi;

public class LiteratureDownloadsResponse
{
    public IList<LiteratureDownload> Downloads { get; set; } = new List<LiteratureDownload>();
}