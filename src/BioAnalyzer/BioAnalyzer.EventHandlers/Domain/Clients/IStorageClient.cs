using BioAnalyzer.EventHandlers.Models;
using BioAnalyzer.AzureStorage.Contracts.Models;

namespace BioAnalyzer.EventHandlers.Domain.Clients;

public interface IStorageClient
{
    Task UploadDocumentAsync(string fileName, DocumentContentType contentType, byte[] fileContent);
    Task SaveDownloadedLiteratureAsync(DownloadedLiterature downloadedLiterature);
    Task SaveDocumentProcessingStatusAsync(DocumentProcessingStatus processingStatus);
}

