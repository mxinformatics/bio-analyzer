using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

public interface ILiteratureService
{
    Task<LiteratureDownloadList> GetDownloadsAsync(CancellationToken cancellationToken = default);
    
    Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken = default);

    Task<LiteratureDownload> UploadManualAsync(
        ManualLiteratureUploadRequest request,
        CancellationToken cancellationToken = default);
}