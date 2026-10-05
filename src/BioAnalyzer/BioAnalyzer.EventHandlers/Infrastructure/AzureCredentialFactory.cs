using Azure.Core;
using Azure.Identity;
using BioAnalyzer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.EventHandlers.Infrastructure;

public class AzureCredentialFactory(
    IOptions<SecureConfiguration> secureConfiguration,
    ILogger<AzureCredentialFactory> logger) : IAzureCredentialFactory
{
    private readonly SecureConfiguration _secureConfiguration = secureConfiguration.Value;

    public TokenCredential Create()
    {
        var options = new DefaultAzureCredentialOptions();

        if (!string.IsNullOrWhiteSpace(_secureConfiguration.IdentityClientId))
        {
            logger.LogInformation("Using Managed Identity with client ID: {ClientId}", _secureConfiguration.IdentityClientId);
            options.ManagedIdentityClientId = _secureConfiguration.IdentityClientId;
            return new DefaultAzureCredential(options);
        }

        logger.LogInformation("Using Default Azure Credential for authentication");
        return new DefaultAzureCredential(options);
    }
}
