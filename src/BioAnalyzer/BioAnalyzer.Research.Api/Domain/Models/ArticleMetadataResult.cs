namespace BioAnalyzer.Research.Api.Domain.Models;

public class ArticleMetadataResult
{
    public string PmcId { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = [];
    public string PublishedDate { get; set; } = string.Empty;
    public List<string> PublicationTypes { get; set; } = [];
}
