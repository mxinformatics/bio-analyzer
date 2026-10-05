using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BioAnalyzer.App.Contracts.Clients;
using BioAnalyzer.App.Contracts.Services;
using BioAnalyzer.App.Services;
using BioAnalyzer.Infrastructure.Configuration;

namespace BioAnalyzer.App.Infrastructure;

public static class BioAnalyzerAppDependencies
{
    public static IServiceCollection AddBioAnalyzerAppDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        
        services.Configure<EventConfiguration>(configuration.GetSection("Events"))
            .PostConfigure<EventConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });

        services.AddScoped<IEventBusClient, EventBusClient>();
        
        services.AddTransient<ResearchApiAuthHandler>();
        services.AddHttpClient<IResearchApiClient, ResearchApiClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://researchapi");
            })
            .AddHttpMessageHandler<ResearchApiAuthHandler>();
        services.AddScoped<ISearchService, SearchService>();
        return services;
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
    
    public static WebApplicationBuilder AddSecureConfiguration(this WebApplicationBuilder builder, IConfiguration configuration)
    {
        var secureConfiguration = builder.Configuration.GetSection("SecureConfiguration").Get<SecureConfiguration>();
        if(secureConfiguration == null)
        {
            throw new InvalidOperationException("SecureConfiguration is required");
        }
        secureConfiguration.ThrowIfInvalid();

        if (secureConfiguration.UseKeyVault)
        {
            var secretClient = new SecretClient(
                new Uri(secureConfiguration.KeyVaultUrl),
                new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions
                    {
                        ExcludeVisualStudioCredential = true,
                        ExcludeEnvironmentCredential = secureConfiguration.ExcludeEnvironmentCredential
                    }
                ));

            builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());    
        }
        return builder;
    }
}