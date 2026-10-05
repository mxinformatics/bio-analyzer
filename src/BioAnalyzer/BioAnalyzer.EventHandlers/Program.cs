using BioAnalyzer.EventHandlers.Infrastructure;
using BioAnalyzer.Infrastructure.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.ConfigureFunctionsWebApplication();

builder.Services.AddHttpClient();
builder.Services.AddEventHandlerDependencies(builder.Configuration);

// Register the credential via AzureCredentialFactory so ManagedIdentityClientId
// from SecureConfiguration:IdentityClientId is applied when populated.
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.UseCredential(sp =>
    {
        var factory = sp.GetRequiredService<IAzureCredentialFactory>();
        return factory.Create();
    });
});

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();