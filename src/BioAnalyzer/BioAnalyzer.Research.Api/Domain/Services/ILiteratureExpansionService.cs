using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Services;

public interface ILiteratureExpansionService
{
    Task<LiteratureCandidatesResult> FindCandidatesAsync(
        LiteratureCandidatesRequest request,
        CancellationToken cancellationToken = default);
}
