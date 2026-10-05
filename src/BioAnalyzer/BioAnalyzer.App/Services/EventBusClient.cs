using Azure.Identity;
using Azure.Messaging.ServiceBus;
using BioAnalyzer.App.Contracts.Clients;
using BioAnalyzer.App.Infrastructure;
using BioAnalyzer.App.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.App.Services;

public class EventBusClient : IEventBusClient
{
    private readonly EventConfiguration _eventConfiguration;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _literatureDownloadSender;
    private readonly ServiceBusSender _documentProcessingSender;
    private readonly ILogger<EventBusClient> _logger;
    private bool _disposed;

    public EventBusClient(IOptions<EventConfiguration> eventConfiguration, ILogger<EventBusClient> logger)
    {
        _eventConfiguration = eventConfiguration.Value;
        _logger = logger;
        
        _logger.LogInformation("Initializing EventBusClient for namespace: {ServiceBusNamespace}, topic: {Topic}",
            _eventConfiguration.ServiceBusNamespace, _eventConfiguration.LiteratureDownloadTopic);
        
        _client = new ServiceBusClient(
            _eventConfiguration.ServiceBusNamespace,
            new DefaultAzureCredential());
        _literatureDownloadSender = _client.CreateSender(_eventConfiguration.LiteratureDownloadTopic);
        _documentProcessingSender = _client.CreateSender(_eventConfiguration.DocumentProcessingRequestedQueue);
    }
    
    public async Task Publish<TMessageType>(IList<TMessageType> messages, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messages, nameof(messages));
        
        if (messages.Count == 0)
        {
            _logger.LogWarning("Attempted to publish empty message list");
            return;
        }
        
        try
        {
            _logger.LogInformation("Publishing {MessageCount} messages of type {MessageType} to Service Bus",
                messages.Count, typeof(TMessageType).Name);
            
            var messageBatch = await _literatureDownloadSender.CreateMessageBatchAsync(cancellationToken);
            foreach (var message in messages)
            {
                if (!messageBatch.TryAddMessage(new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(message))))
                {
                    _logger.LogError("Message is too large to fit in the batch. Message type: {MessageType}", typeof(TMessageType).Name);
                    throw new InvalidOperationException($"Message {message} is too large to fit in the batch.");
                }
            }

            await _literatureDownloadSender.SendMessagesAsync(messageBatch, cancellationToken);
            _logger.LogInformation("Successfully published {MessageCount} messages to Service Bus", messages.Count);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to publish messages to Service Bus");
            throw;
        }
    }

    public async Task PublishDocumentProcessingRequest(
        DocumentProcessingRequest processingRequest,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(processingRequest, nameof(processingRequest));

        var payload = System.Text.Json.JsonSerializer.Serialize(processingRequest);
        await _documentProcessingSender.SendMessageAsync(new ServiceBusMessage(payload), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing EventBusClient");
        
        await _literatureDownloadSender.DisposeAsync();
        await _documentProcessingSender.DisposeAsync();
        await _client.DisposeAsync();
        _disposed = true;
        
        _logger.LogDebug("EventBusClient disposed successfully");
    }
}
