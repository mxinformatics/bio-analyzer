using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.AzureStorage.Contracts.Models;
using BioAnalyzer.EventHandlers.Infrastructure;
using BioAnalyzer.EventHandlers.Models;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.EventHandlers.Domain.Clients;

public class StorageClient(
    ITableContext tableContext,
    IBlobContext blobContext,
    IOptions<EventHandlerConfiguration> storageConfiguration) : IStorageClient
{
    private readonly EventHandlerConfiguration _storageConfiguration = storageConfiguration.Value;

    public async Task UploadDocumentAsync(string fileName, DocumentContentType contentType, byte[] fileContent)
    {
        var document = new ByteDocument(fileName, contentType, fileContent);
        var container = new StorageContainer(_storageConfiguration.DownloadContainerName);
        await blobContext.Upload(document, container).ConfigureAwait(false);
    }

    public async Task SaveDownloadedLiteratureAsync(DownloadedLiterature downloadedLiterature)
    {
        await tableContext
            .UpsertEntityAsync(_storageConfiguration.DownloadTableName, downloadedLiterature)
            .ConfigureAwait(false);
    }

    public async Task SaveDocumentProcessingStatusAsync(DocumentProcessingStatus processingStatus)
    {
        await tableContext
            .UpsertEntityAsync(_storageConfiguration.ProcessingStatusTableName, processingStatus)
            .ConfigureAwait(false);
    }
}

