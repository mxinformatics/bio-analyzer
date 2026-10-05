namespace BioAnalyzer.Infrastructure.Configuration;

/// <summary>
/// Model for reading the secure configuration from the appsettings.json file
/// </summary>
public class SecureConfiguration
{
    public string KeyVaultUrl { get; set; } = string.Empty;
    public bool ExcludeEnvironmentCredential { get; set; } 
    
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
}    
