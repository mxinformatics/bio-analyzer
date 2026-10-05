namespace BioAnalyzer.App.Models.Messages;

public class DocumentProcessingRequest
{
    public string DownloadLink { get; set; } = string.Empty;
    public string XmlDownloadLink { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PmcId { get; set; } = string.Empty;
    public string Doi { get; set; } = string.Empty;
}
