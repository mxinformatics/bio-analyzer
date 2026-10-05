using System.ComponentModel.DataAnnotations;

namespace BioAnalyzer.App.Infrastructure;

public class EventConfiguration
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string ServiceBusNamespace { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string LiteratureDownloadTopic { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string DocumentProcessingRequestedQueue { get; set; } = string.Empty;
    
    public void ThrowIfInvalid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ServiceBusNamespace, nameof(ServiceBusNamespace));
        ArgumentException.ThrowIfNullOrWhiteSpace(LiteratureDownloadTopic, nameof(LiteratureDownloadTopic));
        ArgumentException.ThrowIfNullOrWhiteSpace(DocumentProcessingRequestedQueue, nameof(DocumentProcessingRequestedQueue));
    }
}
