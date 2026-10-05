using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Clients;

/// <summary>
/// Contract for searching the Entrez database.
/// </summary>
public interface IEntrezClient
{
Task<EntrezSearchResult> LiteratureSearchAsync(string query, int startIndex, int retMax = 20, CancellationToken cancellationToken = default);
    
    Task<EntrezSummaryResponse> LiteratureSummaryAsync(IList<string> uids, CancellationToken cancellationToken = default);
}