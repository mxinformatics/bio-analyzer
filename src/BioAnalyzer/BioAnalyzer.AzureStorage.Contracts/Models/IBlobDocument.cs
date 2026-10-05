namespace BioAnalyzer.AzureStorage.Contracts.Models;

/// <summary>
/// Contains the properties of a document stored in Azure Blob Storage.
/// </summary>
public interface IBlobDocument
{
    public string Name { get; set; }
    public string ContentType { get; set; }
    IDictionary<string, string> Metadata { get; set; }
}