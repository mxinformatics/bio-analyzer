using BioAnalyzer.AzureStorage.Contracts;

namespace BioAnalyzer.EventHandlers.Infrastructure;

public class EventHandlerConfiguration : IAzureStorageConfiguration
{
    public string BlobStorageUrl { get; set; } = string.Empty;
    public string TableStorageUrl { get; set; } = string.Empty;
public string DownloadTableName { get; set; } = string.Empty;
    public string ProcessingStatusTableName { get; set; } = string.Empty;
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
        if (string.IsNullOrWhiteSpace(ProcessingStatusTableName))
        {
            throw new ArgumentNullException(nameof(ProcessingStatusTableName));
        }

        if (string.IsNullOrWhiteSpace(DownloadContainerName))
        {
            throw new ArgumentNullException(nameof(DownloadContainerName));
        }
    }
}
