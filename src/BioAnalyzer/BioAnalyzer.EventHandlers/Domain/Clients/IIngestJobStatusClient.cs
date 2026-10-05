namespace BioAnalyzer.EventHandlers.Domain.Clients;

public interface IIngestJobStatusClient
{
    Task MarkItemProgressAsync(
        string jobId,
        string pmcId,
        string itemStatus,
        string stage,
        string? documentId = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}
