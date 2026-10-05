using BioAnalyzer.AzureStorage.Contracts;

namespace BioAnalyzer.Research.Api.Infrastructure;

/// <summary>
/// Configuration for Azure Storage used in the Research API.
/// </summary>
public class ResearchApiStorageConfiguration : IAzureStorageConfiguration
{
    public string BlobStorageUrl { get; set; } = string.Empty;
    public string TableStorageUrl { get; set; } = string.Empty;
public string DownloadTableName { get; set; } = string.Empty;
    public string RetrievalMetricsTableName { get; set; } = string.Empty;
    public string IngestJobTableName { get; set; } = "LiteratureIngestJobs";
    public string DownloadContainerName { get; set; } = string.Empty;
    
    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(BlobStorageUrl))
        {
            throw new ArgumentNullException(nameof(BlobStorageUrl));
        }

        if (string.IsNullOrWhiteSpace(TableStorageUrl))
        {
            throw new ArgumentNullException(nameof(TableStorageUrl));
        }

        if (string.IsNullOrWhiteSpace(DownloadTableName))
        {
            throw new ArgumentNullException(nameof(DownloadTableName));
        }
        if (string.IsNullOrWhiteSpace(RetrievalMetricsTableName))
        {
            throw new ArgumentNullException(nameof(RetrievalMetricsTableName));
        }
        if (string.IsNullOrWhiteSpace(IngestJobTableName))
        {
            throw new ArgumentNullException(nameof(IngestJobTableName));
        }

        if (string.IsNullOrWhiteSpace(DownloadContainerName))
        {
            throw new ArgumentNullException(nameof(DownloadContainerName));
        }
    }
}
