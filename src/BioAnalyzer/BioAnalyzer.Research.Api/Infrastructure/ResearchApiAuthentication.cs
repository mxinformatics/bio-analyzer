using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace BioAnalyzer.Research.Api.Infrastructure;

public static class ResearchApiAuthentication
{
    public const string DefaultPolicyName = "ResearchApiCaller";
    public const string JwtScheme = JwtBearerDefaults.AuthenticationScheme;
    public const string ApiKeyScheme = ApiKeyAuthenticationHandler.SchemeName;

    public static IServiceCollection AddResearchApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<ApiAuthenticationOptions>(configuration.GetSection(ApiAuthenticationOptions.SectionName))
            .PostConfigure<ApiAuthenticationOptions>(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    options.ApiKey = configuration["RESEARCH_API_KEY"] ?? string.Empty;
                }

                options.ThrowIfInvalid();
            });

        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName))
            .PostConfigure<AzureAdOptions>(options =>
            {
                // Resource-app settings may live under ResearchApi:AzureAd so interactive
                // clients can own AzureAd:ClientId (UI app with reply URLs).
                ApplyResearchApiAzureAdOverrides(options, configuration);
                if (string.IsNullOrWhiteSpace(options.Instance))
                {
                    options.Instance = "https://login.microsoftonline.com/";
                }
            });

        var apiKeyOptions = configuration
            .GetSection(ApiAuthenticationOptions.SectionName)
            .Get<ApiAuthenticationOptions>() ?? new ApiAuthenticationOptions();
        if (string.IsNullOrWhiteSpace(apiKeyOptions.ApiKey))
        {
            apiKeyOptions.ApiKey = configuration["RESEARCH_API_KEY"] ?? string.Empty;
        }

        var azureAd = configuration.GetSection(AzureAdOptions.SectionName).Get<AzureAdOptions>()
                      ?? new AzureAdOptions();
        ApplyResearchApiAzureAdOverrides(azureAd, configuration);

        var jwtEnabled = azureAd.IsConfigured;
        var apiKeyEnabled = ApiKeyShouldBeAvailable(apiKeyOptions, environment, jwtEnabled);
        var enforce = jwtEnabled || apiKeyEnabled;

        services.AddSingleton(new ApiAuthenticationRuntimeState(
            Enforced: enforce,
            JwtEnabled: jwtEnabled,
            ApiKeyEnabled: apiKeyEnabled,
            HeaderName: apiKeyOptions.HeaderName));

        if (!enforce)
        {
            services.AddAuthorization(auth =>
            {
                var allowAll = new AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build();
                auth.DefaultPolicy = allowAll;
                auth.FallbackPolicy = null;
                auth.AddPolicy(DefaultPolicyName, allowAll);
            });
            return services;
        }

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = jwtEnabled ? JwtScheme : ApiKeyScheme;
            options.DefaultChallengeScheme = jwtEnabled ? JwtScheme : ApiKeyScheme;
        });

        if (jwtEnabled)
        {
            authBuilder.AddMicrosoftIdentityWebApi(
                jwtOptions =>
                {
                    configuration.Bind(AzureAdOptions.SectionName, jwtOptions);
                    if (!string.IsNullOrWhiteSpace(azureAd.Audience))
                    {
                        jwtOptions.Audience = azureAd.Audience;
                    }

                    jwtOptions.TokenValidationParameters.ValidAudiences =
                        azureAd.BuildValidAudiences().ToArray();
                },
                identityOptions =>
                {
                    configuration.Bind(AzureAdOptions.SectionName, identityOptions);
                },
                jwtBearerScheme: JwtScheme);
        }

        if (apiKeyEnabled)
        {
            authBuilder.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyScheme,
                _ => { });
        }

        var schemes = new List<string>();
        if (jwtEnabled)
        {
            schemes.Add(JwtScheme);
        }

        if (apiKeyEnabled)
        {
            schemes.Add(ApiKeyScheme);
        }

        services.AddAuthorization(auth =>
        {
            auth.AddPolicy(DefaultPolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(schemes.ToArray());
                policy.RequireAuthenticatedUser();
            });
            auth.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(schemes.ToArray())
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    public static bool ApiKeyShouldBeAvailable(
        ApiAuthenticationOptions options,
        IHostEnvironment environment,
        bool jwtEnabled)
    {
        if (!options.Enabled)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return true;
        }

        if (!jwtEnabled && environment.IsDevelopment() && options.BypassWhenApiKeyMissingInDevelopment)
        {
            return false;
        }

        return false;
    }


    /// <summary>
    /// Prefer ResearchApi:AzureAd:* for the API resource app (ClientId/Audience).
    /// AzureAd:ClientId is reserved for interactive UI apps that have reply URLs.
    /// </summary>
    private static void ApplyResearchApiAzureAdOverrides(AzureAdOptions options, IConfiguration configuration)
    {
        var resourceClientId = configuration["ResearchApi:AzureAd:ClientId"];
        var resourceAudience = configuration["ResearchApi:AzureAd:Audience"]
            ?? configuration["AzureAd:Audience"];
        var resourceTenant = configuration["ResearchApi:AzureAd:TenantId"];

        if (!string.IsNullOrWhiteSpace(resourceClientId))
        {
            options.ClientId = resourceClientId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(resourceAudience))
        {
            options.Audience = resourceAudience.Trim();
        }

        if (!string.IsNullOrWhiteSpace(resourceTenant))
        {
            options.TenantId = resourceTenant.Trim();
        }

        // If only Audience (api://...) is set, still treat JWT as configured when TenantId present.
        // ClientId remains preferred for Microsoft.Identity.Web.
    }

    public static bool ShouldEnforce(
        ApiAuthenticationOptions apiKeyOptions,
        AzureAdOptions azureAd,
        IHostEnvironment environment)
    {
        var jwtEnabled = azureAd.IsConfigured;
        var apiKeyEnabled = ApiKeyShouldBeAvailable(apiKeyOptions, environment, jwtEnabled);
        return jwtEnabled || apiKeyEnabled
               || (apiKeyOptions.Enabled && !environment.IsDevelopment());
    }
}

public sealed record ApiAuthenticationRuntimeState(
    bool Enforced,
    bool JwtEnabled,
    bool ApiKeyEnabled,
    string HeaderName);

public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId);

    public IEnumerable<string> BuildValidAudiences()
    {
        if (!string.IsNullOrWhiteSpace(Audience))
        {
            yield return Audience.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ClientId))
        {
            yield return ClientId.Trim();
        }
    }
}
