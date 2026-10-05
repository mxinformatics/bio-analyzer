using System.Text.Json.Serialization;

namespace BioAnalyzer.Research.Api.Domain.Models;

/// <summary>
/// Model for storing the results of an Entrez search.
/// </summary>
public class EntrezSearchResult
{
    [JsonPropertyName("count")]
    public string Count { get; set; } = string.Empty;
    
    [JsonPropertyName("retmax")]
    public string RetMax { get; set; } = string.Empty;
    
    [JsonPropertyName("retstart")]
    public string RetStart { get; set; } = string.Empty;
    
    [JsonPropertyName("idlist")]
    public ICollection<string> IdList { get; set; } = new List<string>();
    
    [JsonPropertyName("translationset")]
    public ICollection<EntrezTranslationSet> TranslationSet { get; set; } = new List<EntrezTranslationSet>();
    
    [JsonPropertyName("querytranslation")]
    public string QueryTranslation { get; set; } = string.Empty;
}