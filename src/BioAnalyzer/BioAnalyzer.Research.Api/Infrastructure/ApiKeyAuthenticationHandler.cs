using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Infrastructure;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<ApiAuthenticationOptions> apiAuthOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = ApiAuthenticationOptions.DefaultSchemeName;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var config = apiAuthOptions.CurrentValue;
        var headerName = string.IsNullOrWhiteSpace(config.HeaderName)
            ? ApiAuthenticationOptions.DefaultHeaderName
            : config.HeaderName;

        if (!Request.Headers.TryGetValue(headerName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {headerName} header."));
        }

        var providedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Empty {headerName} header."));
        }

        var expectedKey = config.ApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is not configured on the server."));
        }

        if (!FixedTimeEquals(providedKey.Trim(), expectedKey.Trim()))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "research-api-client"),
            new Claim(ClaimTypes.AuthenticationMethod, SchemeName),
            new Claim("auth_type", "api_key")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
               && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
