namespace BioAnalyzer.EventHandlers.Models;

public class DownloadRequest
{
    public string PmcId { get; set; } = string.Empty;
    public string DownloadLink { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    
    public string Doi { get; set; } = string.Empty;

    /// <summary>Optional agentic ingest job id for status correlation.</summary>
    public string JobId { get; set; } = string.Empty;
    
    public bool IsPdf  => !string.IsNullOrEmpty(DownloadLink) && DownloadLink.EndsWith("pdf"); 
    public string UploadedFilename => $"{PmcId}.pdf";
    public string GetHttpDownloadLink()
    {
        return !string.IsNullOrEmpty(DownloadLink) ? DownloadLink.Replace("ftp://", "https://") : DownloadLink;
    }
}