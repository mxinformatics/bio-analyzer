using Azure.Core;
using BioAnalyzer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioAnalyzer.Research.Api.Domain.Clients;

public class AzureCredentialFactory(IOptions<SecureConfiguration> secureConfiguration,
    ILogger<AzureCredentialFactory> logger) : IAzureCredentialFactory
{
    private readonly SecureConfiguration _secureConfiguration = secureConfiguration.Value;
    
    public TokenCredential Create()
    {
        if (!string.IsNullOrWhiteSpace(_secureConfiguration.IdentityClientId)
            && !_secureConfiguration.ExcludeManagedIdentityCredential)
        {
            logger.LogInformation("Using Managed Identity with client ID: {ClientId}", _secureConfiguration.IdentityClientId);
        }
        else
        {
            logger.LogInformation(
                "Using Default Azure Credential (ExcludeEnvironment={ExcludeEnvironment}, ExcludeManagedIdentity={ExcludeManagedIdentity})",
                _secureConfiguration.ExcludeEnvironmentCredential,
                _secureConfiguration.ExcludeManagedIdentityCredential);
        }

        return _secureConfiguration.CreateTokenCredential();
    }
}
