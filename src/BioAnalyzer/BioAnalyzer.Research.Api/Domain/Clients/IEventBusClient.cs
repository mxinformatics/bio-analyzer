namespace BioAnalyzer.Research.Api.Domain.Clients;

public interface IEventBusClient
{
    Task PublishLiteratureDownloadRequestsAsync(
        IReadOnlyList<LiteratureDownloadBusMessage> messages,
        CancellationToken cancellationToken = default);
}

public sealed class LiteratureDownloadBusMessage
{
    public string PmcId { get; set; } = string.Empty;

    public string DownloadLink { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Doi { get; set; } = string.Empty;

    public string JobId { get; set; } = string.Empty;
}
