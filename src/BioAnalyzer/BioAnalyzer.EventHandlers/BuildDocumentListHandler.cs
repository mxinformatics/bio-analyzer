using Azure.Messaging.ServiceBus;
using BioAnalyzer.EventHandlers.Domain.Clients;
using BioAnalyzer.EventHandlers.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BioAnalyzer.EventHandlers;

public class BuildDocumentListHandler
{
    private readonly ILogger<BuildDocumentListHandler> _logger;
    private readonly IStorageClient _storageClient;

    public BuildDocumentListHandler(ILogger<BuildDocumentListHandler> logger, IStorageClient storageClient)
    {
        _logger = logger;
        _storageClient = storageClient;
    }

    [Function(nameof(BuildDocumentListHandler))]
    public async Task Run(
        [ServiceBusTrigger("%BuildDocumentListQueue%", Connection = "BioAnalyzerServiceBusListen")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
    
        var downloadedLiterature = message.Body.ToObjectFromJson<DownloadedLiterature>();

        if (downloadedLiterature == null)
        {
            _logger.LogError("Downloaded literature is null for message ID: {id}", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message);
            throw new InvalidOperationException($"Downloaded literature is null for message ID: {message.MessageId}");
        }

        await _storageClient.SaveDownloadedLiteratureAsync(downloadedLiterature).ConfigureAwait(false);
        await messageActions.CompleteMessageAsync(message);
    }
}