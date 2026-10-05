using System.Text;
using BioAnalyzer.App.Contracts.Clients;
using Microsoft.AspNetCore.Components;

namespace BioAnalyzer.App.Components.Pages;

public partial class Chat : ComponentBase, IDisposable
{
    [Inject]
    public IResearchApiClient ResearchApiClient { get; set; } = default!;

    public string Query { get; set; } = string.Empty;

    public string ResponseText => _responseBuilder.ToString();

    private readonly StringBuilder _responseBuilder = new();
    private CancellationTokenSource? _streamCancellationSource;
    private string? _errorMessage;
    private bool _isStreaming;

    private async Task StartStreamAsync()
    {
        if (_isStreaming)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            _errorMessage = "Please enter a question before starting the stream.";
            return;
        }

        _errorMessage = null;
        _responseBuilder.Clear();
        _isStreaming = true;
        _streamCancellationSource = new CancellationTokenSource();

        try
        {
            await foreach (var token in ResearchApiClient.StreamChatQueryAsync(Query, _streamCancellationSource.Token))
            {
                _responseBuilder.Append(token);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // User-triggered cancellation is expected.
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isStreaming = false;
            _streamCancellationSource?.Dispose();
            _streamCancellationSource = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void StopStream()
    {
        _streamCancellationSource?.Cancel();
    }

    private void ClearOutput()
    {
        _errorMessage = null;
        _responseBuilder.Clear();
    }

    public void Dispose()
    {
        _streamCancellationSource?.Cancel();
        _streamCancellationSource?.Dispose();
    }
}

