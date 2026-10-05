using BioAnalyzer.Research.Api.Domain.Clients;
using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

/// <summary>
/// Implementation of the service for searching literature in the Entrez database.
/// </summary>
public class LiteratureSearchService(IEntrezClient client, INcbiClient ncbiClient) : ILiteratureSearchService
{
public async Task<EntrezSearchResult> SearchLiteratureAsync(string query, int startIndex, int retMax = 20, CancellationToken cancellationToken = default)
    {
        return await client.LiteratureSearchAsync(query, startIndex, retMax, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IList<EntrezSummaryResult>> GetLiteratureSummaries(IList<string> uids, CancellationToken cancellationToken = default)
    {
        var response = await client.LiteratureSummaryAsync(uids, cancellationToken).ConfigureAwait(false);
        return response.Results;
    }

    public async Task<ArticleAbstract> GetArticleAbstractAsync(string pmCid, CancellationToken cancellationToken = default)
    {
        var response = await ncbiClient.GetArticleAsync(pmCid, cancellationToken).ConfigureAwait(false);
        return new ArticleAbstract
        {
            Title = response.Title,
            Description = response.Description,
        };
        
    }

    public async Task<LiteratureDownloadLinkResult> GetLiteratureDownloadLinkAsync(string pmCid, CancellationToken cancellationToken = default)
    {
        var response = await ncbiClient.GetLiteratureDownloadLinkAsync(pmCid, cancellationToken).ConfigureAwait(false);
        return new LiteratureDownloadLinkResult
        {
            PmcId = response.PmcId,
            ArchiveLink = response.ArchiveLink,
            PdfLink = response.PdfLink,
        };
    }

    public async Task<ArticleMetadataResult> GetArticleMetadataAsync(string pmcId, CancellationToken cancellationToken = default)
    {
        var response = await ncbiClient.GetArticleAsync(pmcId, cancellationToken).ConfigureAwait(false);
        return new ArticleMetadataResult
        {
            PmcId = pmcId,
            Authors = response.Authors,
            PublishedDate = response.PublishedDate,
            PublicationTypes = response.PublicationTypes
        };
    }
}