using BioAnalyzer.App.Models;
using BioAnalyzer.App.Models.ResearchApi;
using Microsoft.AspNetCore.Components.Forms;

namespace BioAnalyzer.App.Contracts.Services;

public interface ISearchService
{
    Task<LiteratureReferenceList> Search(SearchCriteria criteria, CancellationToken cancellationToken = default);
    
    Task<LiteratureAbstract> GetAbstract(string pmcId, CancellationToken cancellationToken = default);
    
    Task DownloadReference(LiteratureReference reference, CancellationToken cancellationToken = default);

    Task ManualUploadAndProcess(
        LiteratureReference reference,
        IBrowserFile file,
        CancellationToken cancellationToken = default);
    
    Task<IList<LiteratureDownload>> GetDownloads(CancellationToken cancellationToken = default);
    
    Task<byte[]> DownloadFile(string fileName, CancellationToken cancellationToken = default);
}
