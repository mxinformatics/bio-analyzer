namespace BioAnalyzer.App.Models.ResearchApi;

public class LiteratureDownload
{
    public string PmcId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Doi { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DownloadLink { get; set; } = string.Empty;
    public string XmlDownloadLink { get; set; } = string.Empty;
}