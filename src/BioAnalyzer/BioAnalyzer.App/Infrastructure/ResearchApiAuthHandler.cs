using Microsoft.Identity.Web;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.App.Infrastructure;

/// <summary>
/// Attaches an Entra access token (Research.Api scope) or falls back to X-Api-Key.
/// </summary>
public sealed class ResearchApiAuthHandler(
    ITokenAcquisition tokenAcquisition,
    IOptions<AzureAdOptions> azureAdOptions,
    IConfiguration configuration,
    ILogger<ResearchApiAuthHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var scope = azureAdOptions.Value.ResearchApiScope?.Trim();
        if (!string.IsNullOrWhiteSpace(scope))
        {
            try
            {
                var token = await tokenAcquisition
                    .GetAccessTokenForUserAsync([scope])
                    .ConfigureAwait(false);
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed acquiring Entra token for Research.Api; trying API key fallback.");
            }
        }

        var apiKey = FirstNonEmpty(
            configuration["ResearchApi:ApiKey"],
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
