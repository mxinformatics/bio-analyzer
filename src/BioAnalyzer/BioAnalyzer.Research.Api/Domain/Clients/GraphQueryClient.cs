using System.Net.Http.Json;
using System.Net;
using BioAnalyzer.Research.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public class GraphQueryClient(
    HttpClient httpClient,
    IOptions<OpenAiConfiguration> configuration,
    ILogger<GraphQueryClient> logger) : IGraphQueryClient
{
    private static readonly Lock CircuitLock = new();
    private static int _consecutiveFailures;
    private static DateTimeOffset? _circuitOpenUntilUtc;
    private readonly OpenAiConfiguration _configuration = configuration.Value;
    private readonly ILogger<GraphQueryClient> _logger = logger;

    public async Task<IReadOnlyList<GraphQueryResultItem>> QueryAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var request = new GraphQueryRequest
        {
            Query = query,
            TopK = topK
        };
        EnsureCircuitAllowsRequests();
        Exception? lastException = null;

        for (var attempt = 1; attempt <= _configuration.GraphMaxRetryAttempts; attempt++)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "/query")
                {
                    Content = JsonContent.Create(request)
                };

                if (!string.IsNullOrWhiteSpace(_configuration.GraphApiKey))
                {
                    message.Headers.TryAddWithoutValidation("X-API-Key", _configuration.GraphApiKey);
                }

                using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var isTransient = IsTransientStatusCode(response.StatusCode);
                    lastException = new InvalidOperationException(
                        $"Graph query failed with status {(int)response.StatusCode} on attempt {attempt}/{_configuration.GraphMaxRetryAttempts}. Response body: {body}");

                    if (!isTransient)
                    {
                        RegisterFailure(lastException);
                        throw lastException;
                    }
                    if (attempt < _configuration.GraphMaxRetryAttempts)
                    {
                        _logger.LogWarning(lastException, "Transient graph query failure. Retrying attempt {Attempt}.", attempt);
                        await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    break;
                }

                var payload = await response.Content.ReadFromJsonAsync<GraphQueryResponse>(cancellationToken).ConfigureAwait(false);
                RegisterSuccess();
                return payload?.Results ?? [];
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException("Graph query timed out.");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }

            if (attempt < _configuration.GraphMaxRetryAttempts)
            {
                _logger.LogWarning(
                    lastException,
                    "Graph query attempt {Attempt} failed. Retrying.",
                    attempt);
                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        var finalException = lastException ?? new InvalidOperationException("Graph query failed with unknown error.");
        RegisterFailure(finalException);
        throw finalException;
    }

    private void EnsureCircuitAllowsRequests()
    {
        lock (CircuitLock)
        {
            if (_circuitOpenUntilUtc.HasValue && _circuitOpenUntilUtc.Value > DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException(
                    $"Graph query circuit breaker is open until {_circuitOpenUntilUtc.Value:O}");
            }

            if (_circuitOpenUntilUtc.HasValue && _circuitOpenUntilUtc.Value <= DateTimeOffset.UtcNow)
            {
                _circuitOpenUntilUtc = null;
                _consecutiveFailures = 0;
            }
        }
    }

    private void RegisterSuccess()
    {
        lock (CircuitLock)
        {
            _consecutiveFailures = 0;
            _circuitOpenUntilUtc = null;
        }
    }

    private void RegisterFailure(Exception exception)
    {
        lock (CircuitLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures < _configuration.GraphCircuitBreakerFailureThreshold)
            {
                return;
            }

            _circuitOpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(_configuration.GraphCircuitBreakerBreakSeconds);
            _consecutiveFailures = 0;
            _logger.LogError(
                exception,
                "Graph query circuit breaker opened until {OpenUntilUtc}.",
                _circuitOpenUntilUtc.Value);
        }
    }

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var exponentialFactor = Math.Pow(2, attempt - 1);
        var backoffMilliseconds = (int)Math.Min(
            _configuration.GraphInitialBackoffMilliseconds * exponentialFactor,
            5000);
        await Task.Delay(TimeSpan.FromMilliseconds(backoffMilliseconds), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
               || statusCode == HttpStatusCode.TooManyRequests
               || statusCode == HttpStatusCode.InternalServerError
               || statusCode == HttpStatusCode.BadGateway
               || statusCode == HttpStatusCode.ServiceUnavailable
               || statusCode == HttpStatusCode.GatewayTimeout;
    }
}
