using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public sealed class EventBusClient : IEventBusClient, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _literatureDownloadSender;
    private readonly ILogger<EventBusClient> _logger;
    private bool _disposed;

    public EventBusClient(IOptions<EventConfiguration> eventConfiguration, ILogger<EventBusClient> logger)
    {
        var config = eventConfiguration.Value;
        _logger = logger;
        _client = new ServiceBusClient(config.ServiceBusNamespace, new DefaultAzureCredential());
        _literatureDownloadSender = _client.CreateSender(config.LiteratureDownloadTopic);
        _logger.LogInformation(
            "Research.Api EventBusClient initialized for namespace {Namespace}, topic {Topic}",
            config.ServiceBusNamespace,
            config.LiteratureDownloadTopic);
    }

    public async Task PublishLiteratureDownloadRequestsAsync(
        IReadOnlyList<LiteratureDownloadBusMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            return;
        }

        var published = 0;
        var batch = await _literatureDownloadSender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            var payload = JsonSerializer.Serialize(message);
            var sbMessage = new ServiceBusMessage(payload)
            {
                MessageId = $"{message.JobId}:{message.PmcId}",
                Subject = message.PmcId
            };

            if (!batch.TryAddMessage(sbMessage))
            {
                // Flush current batch and start a new one (avoid all-or-nothing on size).
                if (batch.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Literature download message for {message.PmcId} is too large for a Service Bus batch.");
                }

                await _literatureDownloadSender.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
                published += batch.Count;
                batch.Dispose();
                batch = await _literatureDownloadSender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
                if (!batch.TryAddMessage(sbMessage))
                {
                    batch.Dispose();
                    throw new InvalidOperationException(
                        $"Literature download message for {message.PmcId} is too large for a Service Bus batch.");
                }
            }
        }

        if (batch.Count > 0)
        {
            await _literatureDownloadSender.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
            published += batch.Count;
        }

        batch.Dispose();
        _logger.LogInformation("Published {Count} literature download request(s) to Service Bus", published);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _literatureDownloadSender.DisposeAsync().ConfigureAwait(false);
        await _client.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
