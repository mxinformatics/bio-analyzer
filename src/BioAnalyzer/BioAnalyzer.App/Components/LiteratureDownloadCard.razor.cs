using BioAnalyzer.App.Models;
using BioAnalyzer.App.Models.ResearchApi;
using Microsoft.AspNetCore.Components;

namespace BioAnalyzer.App.Components;

public partial class LiteratureDownloadCard : ComponentBase
{
    [Parameter] public LiteratureDownload LiteratureDownload { get; set; } = null!;
    

    [Parameter]
    public EventCallback<string> DownloadFileClicked { get; set; }
    

}