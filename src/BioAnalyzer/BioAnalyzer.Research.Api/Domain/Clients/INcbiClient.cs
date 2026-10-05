using System.Xml;
using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public interface INcbiClient
{
    Task<NcbiArticleResponse> GetArticleAsync(string pmCid, CancellationToken cancellationToken = default);

    Task<NcbiDownloadResponse> GetLiteratureDownloadLinkAsync(string pmcId, CancellationToken cancellationToken = default);
}