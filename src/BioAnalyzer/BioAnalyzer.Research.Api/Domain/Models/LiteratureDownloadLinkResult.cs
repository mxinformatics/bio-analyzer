namespace BioAnalyzer.Research.Api.Domain.Models;

public class LiteratureDownloadLinkResult
{
    public string PmcId { get; set; } = string.Empty;
    public string ArchiveLink { get; set; } = string.Empty;
    public string PdfLink { get; set; } = string.Empty;
}