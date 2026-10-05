namespace BioAnalyzer.EventHandlers.Infrastructure;

/// <summary>
/// Entra client-credentials settings for calling Research.Api as a daemon.
/// Prefer AzureAd section from Key Vault (ResearchDaemon--AzureAd--*).
/// </summary>
public class ResearchApiAuthOptions
{
    public const string SectionName = "ResearchDaemon:AzureAd";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>App ID URI or api://.../.default for client credentials.</summary>
    public string Scope { get; set; } = string.Empty;
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(Scope);
}
