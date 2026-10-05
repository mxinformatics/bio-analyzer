namespace BioAnalyzer.AzureStorage.Contracts;

/// <summary>
/// Configuration interface for Azure Storage.
/// </summary>
public interface IAzureStorageConfiguration
{
    public string BlobStorageUrl { get; }
    public string TableStorageUrl { get; }
}