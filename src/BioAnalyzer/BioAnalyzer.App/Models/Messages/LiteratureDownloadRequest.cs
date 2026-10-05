namespace BioAnalyzer.App.Models.Messages;

public class LiteratureDownloadRequest
{
    public LiteratureDownloadRequest() {}
    public LiteratureDownloadRequest(string pmcId, string downloadLink, string title, string doi)
    {
        PmcId = pmcId;
        DownloadLink = downloadLink;
        Title = title;
        Doi = doi;
    }
    public string PmcId { get; set; } = string.Empty;
    public string DownloadLink { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    
    public string Doi { get; set; } = string.Empty;
}