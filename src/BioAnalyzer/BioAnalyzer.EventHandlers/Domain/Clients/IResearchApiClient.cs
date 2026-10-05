using BioAnalyzer.EventHandlers.Models;

namespace BioAnalyzer.EventHandlers.Domain.Clients;

public interface IResearchApiClient
{
    Task<ArticleMetadata> GetArticleMetadataAsync(string pmcId, CancellationToken cancellationToken = default);
}
