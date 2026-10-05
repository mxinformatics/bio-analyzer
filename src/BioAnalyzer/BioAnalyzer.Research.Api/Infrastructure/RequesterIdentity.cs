using System.Security.Claims;

namespace BioAnalyzer.Research.Api.Infrastructure;

/// <summary>
/// Derives a stable requester identity from the authenticated principal only.
/// Client-supplied body/header values must never be trusted for quota or AuthZ.
/// </summary>
public static class RequesterIdentity
{
    public const string ApiKeySubjectPrefix = "apikey:";
    public const string AppSubjectPrefix = "app:";
    public const string UserSubjectPrefix = "oid:";

    /// <summary>
    /// Resolve a canonical requester id from claims.
    /// Priority: Entra user oid → appid/azp (daemon) → authenticated name (API key subject).
    /// </summary>
    public static string? Resolve(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var oid = FirstClaimValue(
            principal,
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier");
        if (!string.IsNullOrWhiteSpace(oid))
        {
            return UserSubjectPrefix + oid.Trim();
        }

        var appId = FirstClaimValue(principal, "appid", "azp");
        if (!string.IsNullOrWhiteSpace(appId))
        {
            return AppSubjectPrefix + appId.Trim();
        }

        // API key scheme sets ClaimTypes.Name = research-api-client and auth_type=api_key.
        var authType = FirstClaimValue(principal, "auth_type");
        var name = principal.Identity?.Name?.Trim();
        if (string.Equals(authType, "api_key", StringComparison.OrdinalIgnoreCase)
            || string.Equals(principal.Identity?.AuthenticationType, ApiKeyAuthenticationHandler.SchemeName, StringComparison.OrdinalIgnoreCase))
        {
            return ApiKeySubjectPrefix + (string.IsNullOrWhiteSpace(name) ? "client" : name);
        }

        var sub = FirstClaimValue(principal, ClaimTypes.NameIdentifier, "sub");
        if (!string.IsNullOrWhiteSpace(sub))
        {
            return "sub:" + sub.Trim();
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return "name:" + name;
        }

        return null;
    }

    public static bool EqualsRequester(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
    }

    private static string? FirstClaimValue(ClaimsPrincipal principal, params string[] types)
    {
        foreach (var type in types)
        {
            var value = principal.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
