using BioAnalyzer.App.Contracts.Services;
using BioAnalyzer.App.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BioAnalyzer.App.Components;

public partial class DownloadPopup(ISearchService searchService) : ComponentBase
{
    [Parameter]
    public LiteratureReference? LiteratureReference { get; set; }
    
    [Parameter]
    public EventCallback PopupClosed { get; set; }
    
    private LiteratureReference? _literatureReference;
    private string? _currentPmcId;
    private string? _errorMessage;
    private string? _uploadMessage;
    private bool _isDownloading;
    private bool _isUploading;
    private bool _downloadAttempted;
    
    protected override async Task OnParametersSetAsync()
    {
        _literatureReference = LiteratureReference;
        var currentPmcId = _literatureReference?.PmcId;
        if (!string.Equals(_currentPmcId, currentPmcId, StringComparison.Ordinal))
        {
            _currentPmcId = currentPmcId;
            _downloadAttempted = false;
            _errorMessage = null;
            _uploadMessage = null;
        }

        if (_literatureReference is not { CanDownload: true } || _downloadAttempted || _isDownloading)
        {
            return;
        }

        await StartDownloadAsync();
    }

    public async Task Retry()
    {
        await StartDownloadAsync();
    }

    private async Task StartDownloadAsync()
    {
        if (_literatureReference is not { CanDownload: true } || _isDownloading)
        {
            return;
        }

        _downloadAttempted = true;
        _isDownloading = true;
        _errorMessage = null;

        try
        {
            await searchService.DownloadReference(_literatureReference);
            await Close();
        }
        catch (Exception ex)
        {
            _errorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Unable to download the selected article. Please try again."
                : $"Unable to download the selected article. {ex.Message}";
        }
        finally
        {
            _isDownloading = false;
        }
    }

    private async Task OnManualFileSelected(InputFileChangeEventArgs eventArgs)
    {
        if (_literatureReference == null || _isUploading)
        {
            return;
        }

        var selectedFile = eventArgs.File;
        if (selectedFile == null)
        {
            return;
        }

        _isUploading = true;
        _uploadMessage = null;
        try
        {
            await searchService.ManualUploadAndProcess(_literatureReference, selectedFile).ConfigureAwait(false);
            _uploadMessage = "File uploaded successfully. Processing has been queued.";
        }
        catch (Exception ex)
        {
            _uploadMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Unable to upload the file. Please try again."
                : $"Unable to upload the file. {ex.Message}";
        }
        finally
        {
            _isUploading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    
    
    public async Task Close()
    {
        _literatureReference = null;
        _currentPmcId = null;
        _errorMessage = null;
        _uploadMessage = null;
        _isDownloading = false;
        _isUploading = false;
        _downloadAttempted = false;
        await PopupClosed.InvokeAsync();
    }
}