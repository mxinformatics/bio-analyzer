using Azure.Messaging.ServiceBus;
using BioAnalyzer.EventHandlers.Domain.Clients;
using BioAnalyzer.EventHandlers.Models;
using BioAnalyzer.EventHandlers.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BioAnalyzer.EventHandlers;

public class DocumentProcessingOrchestratorHandler(
    ILogger<DocumentProcessingOrchestratorHandler> logger,
    IDocumentProcessingClient documentProcessingClient,
    IResearchApiClient researchApiClient,
    IStorageClient storageClient,
    IIngestJobStatusClient ingestJobStatusClient)
{
    [Function(nameof(DocumentProcessingOrchestratorHandler))]
    public async Task<DocumentProcessingOutputs> Run(
        [ServiceBusTrigger("%DocumentProcessingRequestedQueue%", Connection = "BioAnalyzerServiceBusListen")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var outputs = new DocumentProcessingOutputs();
        logger.LogInformation("Processing message ID: {MessageId}", message.MessageId);

        var downloadedLiterature = message.Body.ToObjectFromJson<DownloadedLiterature>();
        if (downloadedLiterature == null)
        {
            logger.LogError("Downloaded literature payload is null for message ID: {MessageId}", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message).ConfigureAwait(false);
            outputs.ProcessingFailed = new DocumentProcessingFailed
            {
                DocumentId = message.MessageId,
                Stage = "Deserialize",
                ErrorMessage = "Downloaded literature payload is null.",
                DeliveryCount = message.DeliveryCount,
                OccurredUtc = DateTimeOffset.UtcNow
            };
            return outputs;
        }

var documentId = ResolveDocumentId();
        var currentStage = "Received";
        await SaveStatusAsync(downloadedLiterature, documentId, currentStage, "InProgress").ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(downloadedLiterature.JobId))
        {
            await ingestJobStatusClient
                .MarkItemProgressAsync(
                    downloadedLiterature.JobId,
                    downloadedLiterature.PmcId,
                    itemStatus: "Processing",
                    stage: currentStage,
                    documentId: documentId)
                .ConfigureAwait(false);
        }

        try
        {
            currentStage = "Chunk";
            var chunkResponse = await documentProcessingClient
                .ChunkDocumentAsync(downloadedLiterature, documentId)
                .ConfigureAwait(false);
            await SaveStatusAsync(downloadedLiterature, documentId, currentStage, "Completed").ConfigureAwait(false);

            currentStage = "Embed";
            var embedResponse = await documentProcessingClient
                .EmbedDocumentAsync(chunkResponse)
                .ConfigureAwait(false);
            await SaveStatusAsync(downloadedLiterature, documentId, currentStage, "Completed").ConfigureAwait(false);

            currentStage = "Graph";
            var graphResponse = await documentProcessingClient
                .BuildGraphAsync(embedResponse)
                .ConfigureAwait(false);
            await SaveStatusAsync(downloadedLiterature, documentId, currentStage, "Completed").ConfigureAwait(false);

            currentStage = "Enrich";
            try
            {
                var metadata = await researchApiClient
                    .GetArticleMetadataAsync(downloadedLiterature.PmcId)
                    .ConfigureAwait(false);
                await documentProcessingClient
                    .EnrichGraphAsync(graphResponse.DocumentId, metadata)
                    .ConfigureAwait(false);
                await SaveStatusAsync(downloadedLiterature, documentId, currentStage, "Completed").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Graph enrichment failed for document ID: {DocumentId} — continuing without enrichment",
                    documentId);
                await SaveStatusAsync(downloadedLiterature, documentId, currentStage, "Failed", ex.Message).ConfigureAwait(false);
            }

await messageActions.CompleteMessageAsync(message).ConfigureAwait(false);

            logger.LogInformation(
                "Document processing completed for document ID: {DocumentId}. Nodes created: {NodesCreated}",
                graphResponse.DocumentId,
                graphResponse.NodesCreated);

            if (!string.IsNullOrWhiteSpace(downloadedLiterature.JobId))
            {
                await ingestJobStatusClient
                    .MarkItemProgressAsync(
                        downloadedLiterature.JobId,
                        downloadedLiterature.PmcId,
                        itemStatus: "GraphReady",
                        stage: "Graph",
                        documentId: graphResponse.DocumentId)
                    .ConfigureAwait(false);
            }

            outputs.GraphReady = new DocumentGraphReady
            {
                DocumentId = graphResponse.DocumentId,
                PmcId = downloadedLiterature.PmcId,
                Doi = downloadedLiterature.Doi,
                TotalChunks = graphResponse.TotalChunks,
                NodesCreated = graphResponse.NodesCreated,
                CompletedUtc = DateTimeOffset.UtcNow
            };

            return outputs;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Document processing failed for document ID: {DocumentId}, message ID: {MessageId}",
                documentId,
                message.MessageId);

            await SaveStatusAsync(downloadedLiterature, documentId, currentStage, "Failed", ex.Message).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(downloadedLiterature.JobId))
            {
                await ingestJobStatusClient
                    .MarkItemProgressAsync(
                        downloadedLiterature.JobId,
                        downloadedLiterature.PmcId,
                        itemStatus: "Failed",
                        stage: currentStage,
                        documentId: documentId,
                        errorMessage: ex.Message)
                    .ConfigureAwait(false);
            }

            outputs.ProcessingFailed = new DocumentProcessingFailed
            {
                DocumentId = documentId,
                PmcId = downloadedLiterature.PmcId,
                Doi = downloadedLiterature.Doi,
                Stage = currentStage,
                ErrorMessage = ex.Message,
                DeliveryCount = message.DeliveryCount,
                OccurredUtc = DateTimeOffset.UtcNow
            };

            await messageActions.AbandonMessageAsync(message).ConfigureAwait(false);
            return outputs;
        }
    }

    private async Task SaveStatusAsync(
        DownloadedLiterature downloadedLiterature,
        string documentId,
        string stage,
        string status,
        string errorMessage = "")
    {
        var processingStatus = new DocumentProcessingStatus
        {
            RowKey = $"{documentId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{stage}-{Guid.NewGuid():N}",
            DocumentId = documentId,
            PmcId = downloadedLiterature.PmcId,
            Doi = downloadedLiterature.Doi,
            Title = downloadedLiterature.Title,
            Stage = stage,
            Status = status,
            ErrorMessage = errorMessage,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        await storageClient.SaveDocumentProcessingStatusAsync(processingStatus).ConfigureAwait(false);
    }

    private static string ResolveDocumentId()
    {
        return Guid.NewGuid().ToString("D");
    }
}