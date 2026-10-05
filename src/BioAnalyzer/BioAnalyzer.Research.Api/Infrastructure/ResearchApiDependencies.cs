using Azure.AI.Inference;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BioAnalyzer.AzureStorage.Contexts;
using BioAnalyzer.AzureStorage.Contracts;
using BioAnalyzer.Infrastructure.Configuration;
using BioAnalyzer.Research.Api.Domain.Clients;
using BioAnalyzer.Research.Api.Domain.Parsers;
using BioAnalyzer.Research.Api.Domain.Services;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Infrastructure;

public static class ResearchApiDependencies
{
    public static IServiceCollection AddResearchApiDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ResearchApiConfiguration>(configuration.GetSection("ResearchApi"))
            .PostConfigure<ResearchApiConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });
        
services.Configure<OpenAiConfiguration>(configuration.GetSection("OpenAiConfiguration"))
            .PostConfigure<OpenAiConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });

services.Configure<LiteratureExpansionConfiguration>(configuration.GetSection("LiteratureExpansion"))
            .PostConfigure<LiteratureExpansionConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });

        services.Configure<EventConfiguration>(configuration.GetSection("Events"))
            .PostConfigure<EventConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });
        
        services.AddHttpClient<IEntrezClient, EntrezClient>((provider, client) =>
        {
            var apiConfig = provider.GetRequiredService<IOptions<ResearchApiConfiguration>>().Value;
            client.BaseAddress = new Uri(apiConfig.EntrezBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<INcbiClient, NcbiClient>((provider, client) =>
        {
            var apiConfig = provider.GetRequiredService<IOptions<ResearchApiConfiguration>>().Value;
            client.BaseAddress = new Uri(apiConfig.NcbiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddSingleton<IEntrezXmlParser, EntrezXmlParser>();
        services.AddSingleton<INcbiXmlParser, NcbiXmlParser>();
        services.AddSingleton<ChatCompletionsClient>(provider =>
        {
            var openAiConfig = provider.GetRequiredService<IOptions<OpenAiConfiguration>>().Value;
            var credentialFactory = provider.GetRequiredService<IAzureCredentialFactory>();
            var tokenCredential = credentialFactory.Create();
            var clientOptions = new AzureAIInferenceClientOptions();
            var tokenPolicy = new BearerTokenAuthenticationPolicy(
                tokenCredential,
                ["https://cognitiveservices.azure.com/.default"]);
            clientOptions.AddPolicy(tokenPolicy, HttpPipelinePosition.PerRetry);
            return new ChatCompletionsClient(new Uri(openAiConfig.Endpoint), tokenCredential, clientOptions);
        });
        services.AddHttpClient<IGraphQueryClient, GraphQueryClient>((provider, client) =>
        {
            var openAiConfig = provider.GetRequiredService<IOptions<OpenAiConfiguration>>().Value;
            client.BaseAddress = new Uri(openAiConfig.GraphApiUrl);
            client.Timeout = TimeSpan.FromSeconds(openAiConfig.GraphQueryTimeoutSeconds);
        });
        services.AddScoped<IAiClient, AiClient>();
        services.AddScoped<IAIQueryService, AIQueryService>();

        services.Configure<ResearchApiStorageConfiguration>(configuration.GetSection("ResearchStorage"))
            .PostConfigure<ResearchApiStorageConfiguration>(config =>
            {
                config.ThrowIfInvalid();
            });
        
        services.AddScoped<ITableContext, AzureTableContext>((provider) =>
        {
            var storageConfig = provider.GetRequiredService<IOptions<ResearchApiStorageConfiguration>>().Value;
            var credentialFactory = provider.GetRequiredService<IAzureCredentialFactory>();
            return new AzureTableContext(storageConfig, credentialFactory);
        });

        services.AddSingleton<IAzureCredentialFactory, AzureCredentialFactory>();
        services.AddScoped<IBlobContext, AzureBlobContext>((provider) =>
        {
            var storageConfig = provider.GetRequiredService<IOptions<ResearchApiStorageConfiguration>>().Value;
            var credentialFactory = provider.GetRequiredService<IAzureCredentialFactory>();
            return new AzureBlobContext(storageConfig, credentialFactory);
        });
        services.AddScoped<IStorageClient, StorageClient>();
        services.AddScoped<IRetrievalMetricsClient, RetrievalMetricsClient>();
        
services.AddScoped<ILiteratureSearchService, LiteratureSearchService>();
        services.AddScoped<ILiteratureExpansionService, LiteratureExpansionService>();
        services.AddSingleton<IEventBusClient, EventBusClient>();
        services.AddScoped<ILiteratureIngestService, LiteratureIngestService>();
        services.AddScoped<ILiteratureService, LiteratureService>();
        return services;
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