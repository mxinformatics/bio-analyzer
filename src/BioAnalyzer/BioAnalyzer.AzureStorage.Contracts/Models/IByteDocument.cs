namespace BioAnalyzer.AzureStorage.Contracts.Models;

/// <summary>
/// Add binary content to a document.
/// </summary>
public interface IByteDocument : IBlobDocument
{
    public byte[] Content { get; set; }   
}