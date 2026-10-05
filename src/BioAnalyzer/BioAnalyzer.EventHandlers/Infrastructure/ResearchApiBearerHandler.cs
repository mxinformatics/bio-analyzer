using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace BioAnalyzer.EventHandlers.Infrastructure;

/// <summary>
/// Acquires a client-credentials access token for Research.Api, with API-key fallback.
/// </summary>
public sealed class ResearchApiBearerHandler(
    IOptions<ResearchApiAuthOptions> authOptions,
    IConfiguration configuration,
    ILogger<ResearchApiBearerHandler> logger) : DelegatingHandler
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _expiresOn = DateTimeOffset.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = authOptions.Value;
        if (options.IsConfigured)
        {
            try
            {
                var token = await GetTokenAsync(options, cancellationToken).ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed acquiring daemon token for Research.Api; trying API key fallback.");
            }
        }

        var apiKey = FirstNonEmpty(
            configuration["DocumentProcessing:ResearchApiKey"],
            configuration["ApiAuthentication:ApiKey"],
            configuration["RESEARCH_API_KEY"]);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var headerName = configuration["ApiAuthentication:HeaderName"] ?? "X-Api-Key";
            request.Headers.Remove(headerName);
            request.Headers.TryAddWithoutValidation(headerName, apiKey);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTokenAsync(ResearchApiAuthOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTimeOffset.UtcNow < _expiresOn.AddMinutes(-2))
        {
            return _cachedToken!;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTimeOffset.UtcNow < _expiresOn.AddMinutes(-2))
            {
                return _cachedToken!;
            }

            var authority = $"{options.Instance.TrimEnd('/')}/{options.TenantId}";
            var app = ConfidentialClientApplicationBuilder
                .Create(options.ClientId)
                .WithClientSecret(options.ClientSecret)
                .WithAuthority(authority)
                .Build();

            var scope = options.Scope.Trim();
            if (!scope.EndsWith("/.default", StringComparison.OrdinalIgnoreCase))
            {
                // Client credentials require the .default scope form for the resource.
                // Accept either api://app/.default or api://app/access_as_user → convert to .default.
                var slash = scope.LastIndexOf('/');
                scope = slash > 0 ? scope[..slash] + "/.default" : scope + "/.default";
            }

            var result = await app
                .AcquireTokenForClient([scope])
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            _cachedToken = result.AccessToken;
            _expiresOn = result.ExpiresOn;
            return _cachedToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
