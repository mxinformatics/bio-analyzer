namespace BioAnalyzer.Research.Api.Infrastructure;

public class EventConfiguration
{
    public string ServiceBusNamespace { get; set; } = string.Empty;

    public string LiteratureDownloadTopic { get; set; } = string.Empty;

    public void ThrowIfInvalid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ServiceBusNamespace, nameof(ServiceBusNamespace));
        ArgumentException.ThrowIfNullOrWhiteSpace(LiteratureDownloadTopic, nameof(LiteratureDownloadTopic));
    }
}
