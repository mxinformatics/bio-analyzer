using BioAnalyzer.App.Contracts.Services;
using BioAnalyzer.App.Models;
using BioAnalyzer.App.Models.ResearchApi;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BioAnalyzer.App.Components;

public partial class LiteratureDownloadList(ISearchService searchService) : ComponentBase
{
    [Inject]
    IJSRuntime JsRuntime { get; set; } = null!;
    public IList<LiteratureDownload> Downloads { get; set; } = new List<LiteratureDownload>();

 
    protected override async Task OnInitializedAsync()
    {
        Downloads = await searchService.GetDownloads().ConfigureAwait(false);
    }
    
    public async Task DownloadFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var response = await searchService.DownloadFile(fileName).ConfigureAwait(false);
        var responseStream = new MemoryStream(response);
        using var streamRef = new DotNetStreamReference(responseStream);
        await JsRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef).ConfigureAwait(false);
    }


}