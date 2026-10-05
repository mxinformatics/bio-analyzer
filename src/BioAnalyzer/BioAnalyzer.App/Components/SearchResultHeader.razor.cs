using BioAnalyzer.App.Models.ResearchApi;
using Microsoft.AspNetCore.Components;

namespace BioAnalyzer.App.Components;

public partial class SearchResultHeader : ComponentBase
{
    
    [Parameter]
    public EventCallback<string> SearchNextClicked { get; set; }
    
    [Parameter]
    public LiteratureReferenceList SearchResults { get; set; } = new LiteratureReferenceList();
}