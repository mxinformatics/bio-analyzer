using System.ComponentModel.DataAnnotations;

namespace BioAnalyzer.App.Models;

public class LiteratureAbstract
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
}
