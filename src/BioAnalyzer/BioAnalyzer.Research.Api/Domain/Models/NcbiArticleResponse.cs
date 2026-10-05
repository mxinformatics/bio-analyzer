namespace BioAnalyzer.Research.Api.Domain.Models;

public class NcbiArticleResponse
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = [];
    public string PublishedDate { get; set; } = string.Empty;
    public List<string> PublicationTypes { get; set; } = [];
}