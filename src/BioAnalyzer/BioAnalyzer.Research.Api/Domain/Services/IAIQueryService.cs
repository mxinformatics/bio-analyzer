using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

// ReSharper disable once InconsistentNaming
public interface IAIQueryService
{
    Task<string> QueryAsync(string query, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamQueryAsync(string query, CancellationToken cancellationToken = default);

    Task<ChatQueryResult> QueryStructuredAsync(string query, CancellationToken cancellationToken = default);
}
