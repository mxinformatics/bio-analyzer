using System.Text.Json.Serialization;

namespace BioAnalyzer.EventHandlers.Models;

public class ArticleMetadata
{
    [JsonPropertyName("pmc_id")]
    public string PmcId { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public List<string> Authors { get; set; } = [];

    [JsonPropertyName("published_date")]
    public string PublishedDate { get; set; } = string.Empty;

    [JsonPropertyName("publication_types")]
    public List<string> PublicationTypes { get; set; } = [];
}
