using System.Text.Json.Serialization;

namespace BioAnalyzer.Research.Api.Domain.Models;


/// <summary>
///  Model to return the translation of query terms from Entrez. It contains the original query term and its corresponding translation.
/// </summary>
public class EntrezTranslationSet
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
}