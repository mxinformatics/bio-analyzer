using System.Net.Http.Headers;

namespace BioAnalyzer.Research.Api.Domain.Clients;

internal static class NcbiRequestCoordinator
{
    private static readonly SemaphoreSlim _requestLock = new(1, 1);
    private static readonly TimeSpan _minimumRequestInterval = TimeSpan.FromMilliseconds(350);
    private static DateTimeOffset _nextAllowedRequestTimeUtc = DateTimeOffset.MinValue;

    public static async Task WaitForRequestSlotAsync(CancellationToken cancellationToken = default)
    {
        TimeSpan delay;
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var scheduledRequestTime = _nextAllowedRequestTimeUtc > now
                ? _nextAllowedRequestTimeUtc
                : now;
            delay = scheduledRequestTime - now;
            _nextAllowedRequestTimeUtc = scheduledRequestTime + _minimumRequestInterval;
        }
        finally
        {
            _requestLock.Release();
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    public static TimeSpan GetRetryDelay(RetryConditionHeaderValue? retryAfterHeader, int attempt)
    {
        if (retryAfterHeader?.Delta is { } retryDelta && retryDelta > TimeSpan.Zero)
        {
            return retryDelta;
        }

        if (retryAfterHeader?.Date is { } retryDate)
        {
            var dateDelay = retryDate - DateTimeOffset.UtcNow;
            if (dateDelay > TimeSpan.Zero)
            {
                return dateDelay;
            }
        }

        var backoffSeconds = Math.Min(8, Math.Pow(2, attempt + 1));
        return TimeSpan.FromSeconds(backoffSeconds);
    }
}
