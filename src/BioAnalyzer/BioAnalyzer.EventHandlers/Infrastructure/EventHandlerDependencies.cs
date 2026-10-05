using BioAnalyzer.AzureStorage.Contexts;
using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.EventHandlers.Domain.Clients;
using BioAnalyzer.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.EventHandlers.Infrastructure;

public static class EventHandlerDependencies
{
    public static IServiceCollection AddEventHandlerDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EventHandlerConfiguration>(configuration.GetSection("EventHandlerStorage"))
            .PostConfigure<EventHandlerConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });

        services.Configure<SecureConfiguration>(configuration.GetSection("SecureConfiguration"));
        services.Configure<DocumentProcessingConfiguration>(configuration.GetSection("DocumentProcessing"))
            .PostConfigure<DocumentProcessingConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });

        services.Configure<ResearchApiAuthOptions>(configuration.GetSection(ResearchApiAuthOptions.SectionName))
            .PostConfigure<ResearchApiAuthOptions>(options =>
            {
                // Allow flat env/KeyVault aliases used by terraform secret names.
                if (string.IsNullOrWhiteSpace(options.TenantId))
                {
                    options.TenantId = configuration["AzureAd:TenantId"] ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(options.ClientId))
                {
                    options.ClientId = configuration["ResearchDaemon:AzureAd:ClientId"]
                        ?? configuration["ResearchDaemon--AzureAd--ClientId"]
                        ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                {
                    options.ClientSecret = configuration["ResearchDaemon:AzureAd:ClientSecret"]
                        ?? configuration["ResearchDaemon--AzureAd--ClientSecret"]
                        ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(options.Scope))
                {
                    options.Scope = configuration["ResearchApi:Scope"]
                        ?? configuration["ResearchApi--Scope"]
                        ?? configuration["AzureAd:Audience"]
                        ?? string.Empty;
                }
            });

        services.AddSingleton<IAzureCredentialFactory, AzureCredentialFactory>();

        services.AddScoped<ITableContext, AzureTableContext>(provider =>
        {
            var storageConfig = provider.GetRequiredService<IOptions<EventHandlerConfiguration>>().Value;
            var credentialFactory = provider.GetRequiredService<IAzureCredentialFactory>();
            return new AzureTableContext(storageConfig, credentialFactory);
        });

        services.AddScoped<IBlobContext, AzureBlobContext>(provider =>
        {
            var storageConfig = provider.GetRequiredService<IOptions<EventHandlerConfiguration>>().Value;
            var credentialFactory = provider.GetRequiredService<IAzureCredentialFactory>();
            return new AzureBlobContext(storageConfig, credentialFactory);
        });

services.AddScoped<IStorageClient, StorageClient>();
        services.AddScoped<IIngestJobStatusClient, IngestJobStatusClient>();
        services.AddHttpClient(DocumentProcessingClient.ChunkClientName, (provider, client) =>
        {
            var config = provider.GetRequiredService<IOptions<DocumentProcessingConfiguration>>().Value;
            client.BaseAddress = new Uri(config.ChunkApiUrl);
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        services.AddHttpClient(DocumentProcessingClient.EmbeddingClientName, (provider, client) =>
        {
            var config = provider.GetRequiredService<IOptions<DocumentProcessingConfiguration>>().Value;
            client.BaseAddress = new Uri(config.EmbeddingApiUrl);
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        services.AddHttpClient(DocumentProcessingClient.GraphClientName, (provider, client) =>
        {
            var config = provider.GetRequiredService<IOptions<DocumentProcessingConfiguration>>().Value;
            client.BaseAddress = new Uri(config.GraphDataApiUrl);
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        services.AddHttpClient(DocumentProcessingClient.GraphEnrichClientName, (provider, client) =>
        {
            var config = provider.GetRequiredService<IOptions<DocumentProcessingConfiguration>>().Value;
            client.BaseAddress = new Uri(config.GraphDataApiUrl);
            client.Timeout = TimeSpan.FromMinutes(1);
        });
        services.AddTransient<ResearchApiBearerHandler>();
        services.AddHttpClient<IResearchApiClient, ResearchApiClient>((provider, client) =>
            {
                var config = provider.GetRequiredService<IOptions<DocumentProcessingConfiguration>>().Value;
                client.BaseAddress = new Uri(config.ResearchApiUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<ResearchApiBearerHandler>();
        services.AddScoped<IDocumentProcessingClient, DocumentProcessingClient>();
        return services;
    }
}

