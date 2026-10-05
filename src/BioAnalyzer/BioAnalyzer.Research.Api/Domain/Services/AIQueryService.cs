using BioAnalyzer.Research.Api.Domain.Clients;
using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

// ReSharper disable once InconsistentNaming
public class AIQueryService(IAiClient aiClient) : IAIQueryService
{
    public async Task<string> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        return await aiClient.QueryAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<string> StreamQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        return aiClient.StreamQueryAsync(query, cancellationToken);
    }

    public async Task<ChatQueryResult> QueryStructuredAsync(string query, CancellationToken cancellationToken = default)
    {
        return await aiClient.QueryStructuredAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
