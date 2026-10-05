using BioAnalyzer.App.Models;
using BioAnalyzer.App.Models.ResearchApi;

namespace BioAnalyzer.App.Contracts.Clients;

public interface IResearchApiClient
{
    Task<LiteratureSearchResult> GetLiteratureReferences(string searchTerm, int startIndex, CancellationToken cancellationToken = default);

    Task<IList<LiteratureSummaryResult>> GetLiteratureSummary(IList<string> ids, CancellationToken cancellationToken = default);
    
    Task<LiteratureAbstract> GetLiteratureAbstract(string pmcId, CancellationToken cancellationToken = default);
    
    Task<LiteratureDownloadLinkResponse> DownloadReference(LiteratureReference reference, CancellationToken cancellationToken = default);
    
    Task<LiteratureDownloadsResponse> GetDownloads(CancellationToken cancellationToken = default);
    
    Task<byte[]> DownloadFile(string fileName, CancellationToken cancellationToken = default);

    Task<LiteratureDownload> UploadManualReference(
        string title,
        string pmcId,
        string doi,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatQueryAsync(string query, CancellationToken cancellationToken = default);
}
