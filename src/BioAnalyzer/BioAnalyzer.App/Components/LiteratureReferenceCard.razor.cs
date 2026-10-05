using BioAnalyzer.App.Models;
using Microsoft.AspNetCore.Components;

namespace BioAnalyzer.App.Components;

public partial class LiteratureReferenceCard : ComponentBase
{
    [Parameter] public LiteratureReference LitReference { get; set; } = null!;
    
    [Parameter]
    public EventCallback<LiteratureReference> AbstractQuickViewClicked { get; set; }
    
    [Parameter]
    public EventCallback<LiteratureReference> DownloadReferenceClicked { get; set; }

}