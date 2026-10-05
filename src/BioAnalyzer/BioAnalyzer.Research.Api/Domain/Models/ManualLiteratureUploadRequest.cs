using Microsoft.AspNetCore.Http;

namespace BioAnalyzer.Research.Api.Domain.Models;

public class ManualLiteratureUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string? PmcId { get; set; }
    public string? Doi { get; set; }
    public IFormFile? File { get; set; }
}
