using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public interface IAiClient
{
    Task<string> QueryAsync(string query, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamQueryAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Graph-RAG query with machine-readable status for agent tools.
    /// </summary>
    Task<ChatQueryResult> QueryStructuredAsync(string query, CancellationToken cancellationToken = default);
}
