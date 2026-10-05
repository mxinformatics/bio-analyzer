using BioAnalyzer.App.Models.Messages;

namespace BioAnalyzer.App.Contracts.Clients;

public interface IEventBusClient : IAsyncDisposable
{
    Task Publish<TMessageType>(IList<TMessageType> messages, CancellationToken cancellationToken = default);

    Task PublishDocumentProcessingRequest(
        DocumentProcessingRequest processingRequest,
        CancellationToken cancellationToken = default);
}
