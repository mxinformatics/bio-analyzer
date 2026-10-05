using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

/// <summary>
/// Contract for searching literature in the Entrez database.
/// </summary>
public interface ILiteratureSearchService
{
Task<EntrezSearchResult> SearchLiteratureAsync(string query, int startIndex, int retMax = 20, CancellationToken cancellationToken = default);
    Task<IList<EntrezSummaryResult>> GetLiteratureSummaries(IList<string> uids, CancellationToken cancellationToken = default);
    
    Task<ArticleAbstract> GetArticleAbstractAsync(string pmCid, CancellationToken cancellationToken = default);
    
    Task<LiteratureDownloadLinkResult> GetLiteratureDownloadLinkAsync(string pmCid, CancellationToken cancellationToken = default);

    Task<ArticleMetadataResult> GetArticleMetadataAsync(string pmcId, CancellationToken cancellationToken = default);
}