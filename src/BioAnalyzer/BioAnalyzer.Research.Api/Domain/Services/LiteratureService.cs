using BioAnalyzer.Research.Api.Domain.Clients;
using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

public class LiteratureService(IStorageClient storageClient) : ILiteratureService
{
    public async Task<LiteratureDownloadList> GetDownloadsAsync(CancellationToken cancellationToken = default)
    {
        return await storageClient.GetDownloadsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return await storageClient.DownloadFileAsync(fileName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LiteratureDownload> UploadManualAsync(
        ManualLiteratureUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return await storageClient.UploadManualAsync(request, cancellationToken).ConfigureAwait(false);
    }
}