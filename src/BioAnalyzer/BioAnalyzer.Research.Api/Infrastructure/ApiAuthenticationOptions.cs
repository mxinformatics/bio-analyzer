namespace BioAnalyzer.Research.Api.Infrastructure;

/// <summary>
/// Service-to-service authentication for Research.Api (Phase B).
/// Callers send the shared key in <see cref="HeaderName"/> (default X-Api-Key).
/// </summary>
public class ApiAuthenticationOptions
{
    public const string SectionName = "ApiAuthentication";
    public const string DefaultSchemeName = "ApiKey";
    public const string DefaultHeaderName = "X-Api-Key";

    /// <summary>
    /// When false, authentication is not registered as required (all requests anonymous).
    /// Prefer leaving enabled and using a development key or BypassWhenApiKeyMissingInDevelopment.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Shared secret expected from trusted callers (chat BFF, Blazor App, EventHandlers).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>HTTP header carrying the API key.</summary>
    public string HeaderName { get; set; } = DefaultHeaderName;

    /// <summary>
    /// When true and the host environment is Development and <see cref="ApiKey"/> is empty,
    /// authentication is not enforced (logged). Never rely on this outside local Aspire.
    /// </summary>
    public bool BypassWhenApiKeyMissingInDevelopment { get; set; } = true;

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(HeaderName))
        {
            throw new InvalidOperationException("ApiAuthentication:HeaderName must be non-empty.");
        }
    }
}
