using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

public interface ILiteratureIngestService
{
    Task<LiteratureIngestResponse> RequestIngestAsync(
        LiteratureIngestRequest request,
        CancellationToken cancellationToken = default);

Task<LiteratureIngestJobStatusResult?> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<LiteratureIngestJobStatusResult?> GetJobStatusAsync(
        string jobId,
        string? requesterId,
        CancellationToken cancellationToken = default);
}
