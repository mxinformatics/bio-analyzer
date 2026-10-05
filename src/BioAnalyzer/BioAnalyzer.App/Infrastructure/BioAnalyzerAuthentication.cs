using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace BioAnalyzer.App.Infrastructure;

/// <summary>
/// Configuration extensions for setting up Azure Entra ID authentication.
/// </summary>
public static class BioAnalyzerAuthentication
{
    public static IServiceCollection AddBioAnalyzerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var azureAdOptions = configuration.GetSection("AzureAd").Get<AzureAdOptions>()
                             ?? throw new InvalidOperationException("AzureAd configuration is missing or invalid.");

        services.Configure<AzureAdOptions>(configuration.GetSection("AzureAd"));

        services
            .AddAuthentication(authOptions =>
            {
                authOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddMicrosoftIdentityWebApp(options =>
            {
                configuration.Bind("AzureAd", options);
                options.CallbackPath = string.IsNullOrWhiteSpace(azureAdOptions.CallbackPath)
                    ? "/signin-oidc"
                    : azureAdOptions.CallbackPath;
            })
            .EnableTokenAcquisitionToCallDownstreamApi(BuildInitialScopes(azureAdOptions))
            .AddInMemoryTokenCaches();

        // Keep cookie scheme available for Blazor interactive server.
        services.Configure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme,
            _ => { });

        return services;
    }

    private static IEnumerable<string> BuildInitialScopes(AzureAdOptions options)
    {
        yield return "openid";
        yield return "profile";
        yield return "email";
        yield return "offline_access";
        if (!string.IsNullOrWhiteSpace(options.ResearchApiScope))
        {
            yield return options.ResearchApiScope.Trim();
        }
    }
}
