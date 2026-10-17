using Azure.Core;
using Azure.Identity;

namespace BioAnalyzer.Infrastructure.Configuration;

/// <summary>
/// Model for reading the secure configuration from the appsettings.json file
/// </summary>
public class SecureConfiguration
{
    public string KeyVaultUrl { get; set; } = string.Empty;

    /// <summary>
    /// When true, skips EnvironmentCredential in the DefaultAzureCredential chain.
    /// </summary>
    public bool ExcludeEnvironmentCredential { get; set; }

    /// <summary>
    /// When true, skips ManagedIdentityCredential in the DefaultAzureCredential chain.
    /// Prefer true for local development so IMDS probes do not delay startup.
    /// Leave false in Azure-hosted environments that rely on managed identity.
    /// </summary>
    public bool ExcludeManagedIdentityCredential { get; set; }

    /// <summary>
    /// When true configuration will be read from Azure Key Vault
    /// </summary>
    public bool UseKeyVault { get; set; }

    public string IdentityClientId { get; set; } = string.Empty;

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(KeyVaultUrl))
        {
            throw new ArgumentNullException(nameof(KeyVaultUrl));
        }
    }

    public DefaultAzureCredentialOptions CreateDefaultAzureCredentialOptions()
    {
        var options = new DefaultAzureCredentialOptions
        {
            ExcludeVisualStudioCredential = true,
            ExcludeEnvironmentCredential = ExcludeEnvironmentCredential,
            ExcludeManagedIdentityCredential = ExcludeManagedIdentityCredential
        };

        if (!string.IsNullOrWhiteSpace(IdentityClientId))
        {
            options.ManagedIdentityClientId = IdentityClientId;
        }

        return options;
    }

    public TokenCredential CreateTokenCredential() =>
        new DefaultAzureCredential(CreateDefaultAzureCredentialOptions());
}
