using BioAnalyzer.AzureStorage.Contracts;

namespace BioAnalyzer.AzureStorage.Configuration;

/// <summary>
/// Configuration settings for Azure Storage.
/// </summary>
public class AzureStorageConfiguration(string blobStorageUrl, string tableStorageUrl) : IAzureStorageConfiguration
{
    public string BlobStorageUrl { get; } =  blobStorageUrl;

    public string TableStorageUrl { get; set; } = tableStorageUrl;
}