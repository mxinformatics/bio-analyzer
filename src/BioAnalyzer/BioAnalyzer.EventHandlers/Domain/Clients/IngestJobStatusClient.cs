using System.Text.Json;
using Azure;
using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.EventHandlers.Infrastructure;
using BioAnalyzer.EventHandlers.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.EventHandlers.Domain.Clients;

public class IngestJobStatusClient(
    ITableContext tableContext,
    IOptions<EventHandlerConfiguration> storageConfiguration,
    ILogger<IngestJobStatusClient> logger) : IIngestJobStatusClient
{
    private const int MaxOptimisticRetries = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly EventHandlerConfiguration _config = storageConfiguration.Value;
    private readonly ILogger<IngestJobStatusClient> _logger = logger;

    public async Task MarkItemProgressAsync(
        string jobId,
        string pmcId,
        string itemStatus,
        string stage,
        string? documentId = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId) || string.IsNullOrWhiteSpace(pmcId))
        {
            return;
        }

        var tableName = string.IsNullOrWhiteSpace(_config.IngestJobTableName)
            ? "LiteratureIngestJobs"
            : _config.IngestJobTableName;

        var normalizedPmc = NormalizePmcId(pmcId);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxOptimisticRetries; attempt++)
        {
            try
            {
                var job = await tableContext
                    .GetEntityAsync<LiteratureIngestJobEntity>(
                        tableName,
                        LiteratureIngestJobEntity.DefaultPartitionKey,
                        jobId.Trim())
                    .ConfigureAwait(false);

                if (job is null)
                {
                    _logger.LogDebug("No ingest job {JobId} found when updating {PmcId}", jobId, pmcId);
                    return;
                }

                var ifMatch = job.ETag;
                var items = DeserializeItems(job.ItemsJson);
                var item = items.FirstOrDefault(i =>
                    string.Equals(NormalizePmcId(i.PmcId), normalizedPmc, StringComparison.OrdinalIgnoreCase));

                if (item is null)
                {
                    item = new IngestItemStatus
                    {
                        PmcId = normalizedPmc
                    };
                    items.Add(item);
                }

                item.Status = itemStatus;
                item.Stage = stage;
                if (!string.IsNullOrWhiteSpace(documentId))
                {
                    item.DocumentId = documentId;
                }

                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    item.ErrorMessage = errorMessage;
                }

                job.ItemsJson = JsonSerializer.Serialize(items, JsonOptions);
                job.Status = DeriveJobStatus(items);
                job.UpdatedUtc = DateTimeOffset.UtcNow;
                if (!string.IsNullOrWhiteSpace(errorMessage) &&
                    string.Equals(itemStatus, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    job.ErrorMessage = errorMessage;
                }

                await tableContext
                    .ReplaceEntityAsync(tableName, job, ifMatch)
                    .ConfigureAwait(false);

                if (attempt > 1)
                {
                    _logger.LogInformation(
                        "Ingest job {JobId} item {PmcId} updated after {Attempt} optimistic retries",
                        jobId,
                        normalizedPmc,
                        attempt);
                }

                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 412 or 409)
            {
                lastError = ex;
                _logger.LogDebug(
                    ex,
                    "Optimistic concurrency conflict updating ingest job {JobId} for {PmcId} (attempt {Attempt})",
                    jobId,
                    normalizedPmc,
                    attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogError(ex, "Failed updating ingest job {JobId} for {PmcId}", jobId, pmcId);
                throw;
            }
        }

        _logger.LogError(
            lastError,
            "Exhausted optimistic concurrency retries updating ingest job {JobId} for {PmcId}",
            jobId,
            normalizedPmc);
        throw new InvalidOperationException(
            $"Failed to update ingest job {jobId} for {normalizedPmc} after {MaxOptimisticRetries} concurrency retries.",
            lastError);
    }

    /// <summary>Visible for unit tests — job rollup from item statuses.</summary>
    internal static string DeriveJobStatus(IReadOnlyList<IngestItemStatus> items)
    {
        if (items.Count == 0)
        {
            return "Accepted";
        }

        var active = items
            .Where(i => !string.Equals(i.Status, "Skipped", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (active.Count == 0)
        {
            return "Failed";
        }

        var graphReady = active.Count(i => string.Equals(i.Status, "GraphReady", StringComparison.OrdinalIgnoreCase));
        var failed = active.Count(i => string.Equals(i.Status, "Failed", StringComparison.OrdinalIgnoreCase));
        var processing = active.Count(i =>
            string.Equals(i.Status, "Processing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.Status, "Downloading", StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.Status, "Accepted", StringComparison.OrdinalIgnoreCase));

        if (graphReady == active.Count)
        {
            return "GraphReady";
        }

        if (failed == active.Count)
        {
            return "Failed";
        }

        if (graphReady > 0 && (failed > 0 || processing > 0))
        {
            return "Partial";
        }

        if (processing > 0)
        {
            return active.Any(i => string.Equals(i.Status, "Processing", StringComparison.OrdinalIgnoreCase))
                ? "Processing"
                : "Downloading";
        }

        return "Accepted";
    }

    private static List<IngestItemStatus> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<IngestItemStatus>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizePmcId(string pmcId)
    {
        if (string.IsNullOrWhiteSpace(pmcId))
        {
            return string.Empty;
        }

        var trimmed = pmcId.Trim();
        return trimmed.StartsWith("PMC", StringComparison.OrdinalIgnoreCase)
            ? "PMC" + trimmed[3..]
            : "PMC" + trimmed;
    }

    internal sealed class IngestItemStatus
    {
        public string PmcId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Stage { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DocumentId { get; set; }
    }
}
