using System.ComponentModel.DataAnnotations;

namespace BioAnalyzer.App.Models;

public class SearchCriteria
{
    [StringLength(500)]
    public string? SearchTerm { get; set; }
    
    [Range(0, int.MaxValue)]
    public int StartIndex { get; set; }
}
