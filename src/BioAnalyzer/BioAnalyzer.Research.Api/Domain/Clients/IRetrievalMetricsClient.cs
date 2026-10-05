using BioAnalyzer.Research.Api.Domain.Models;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public interface IRetrievalMetricsClient
{
    Task SaveAsync(RetrievalEvaluationMetric metric, CancellationToken cancellationToken = default);
}
