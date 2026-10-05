namespace BioAnalyzer.Research.Api.Domain.Models;

public class LiteratureDownloadList
{
    public IList<LiteratureDownload> Downloads { get; set; } = new List<LiteratureDownload>();
}