using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.Research.Api.Domain.DataTransfer;
using BioAnalyzer.Research.Api.Domain.Models;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public class RetrievalMetricsClient(
    ITableContext tableContext,
    IOptions<ResearchApiStorageConfiguration> storageConfiguration) : IRetrievalMetricsClient
{
    private readonly ResearchApiStorageConfiguration _storageConfiguration = storageConfiguration.Value;

    public async Task SaveAsync(RetrievalEvaluationMetric metric, CancellationToken cancellationToken = default)
    {
        var dto = new RetrievalEvaluationMetricDto
        {
            PartitionKey = metric.EvaluatedUtc.UtcDateTime.ToString("yyyyMMdd"),
            RowKey = Guid.NewGuid().ToString("N"),
            QueryHash = metric.QueryHash,
            QueryPreview = metric.QueryPreview,
            Status = metric.Status,
            TotalResults = metric.TotalResults,
            SelectedResults = metric.SelectedResults,
            MinScore = metric.MinScore,
            AvgScore = metric.AvgScore,
            MaxScore = metric.MaxScore,
            EvidenceThreshold = metric.EvidenceThreshold,
            TopK = metric.TopK,
            MinEvidenceCount = metric.MinEvidenceCount,
            MaxEvidenceCount = metric.MaxEvidenceCount,
            RetrievalDurationMs = metric.RetrievalDurationMs,
            ErrorMessage = metric.ErrorMessage
        };

        await tableContext
            .UpsertEntityAsync(_storageConfiguration.RetrievalMetricsTableName, dto)
            .ConfigureAwait(false);
    }
}
