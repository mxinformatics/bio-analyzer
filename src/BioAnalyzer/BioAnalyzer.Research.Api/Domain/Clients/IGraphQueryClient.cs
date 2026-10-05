namespace BioAnalyzer.Research.Api.Domain.Clients;

public interface IGraphQueryClient
{
    Task<IReadOnlyList<GraphQueryResultItem>> QueryAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default);
}
