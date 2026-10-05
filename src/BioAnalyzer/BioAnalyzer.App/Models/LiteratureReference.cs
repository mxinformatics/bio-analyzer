using System.ComponentModel.DataAnnotations;

namespace BioAnalyzer.App.Models;

public class LiteratureReference
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string PmcId { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string Doi { get; set; } = string.Empty;

    public bool CanDownload => !string.IsNullOrWhiteSpace(PmcId);
    public bool HasDoi => !string.IsNullOrWhiteSpace(Doi);
    public string DoiUrl => HasDoi ? $"https://doi.org/{Uri.EscapeDataString(Doi.Trim())}" : string.Empty;
}
