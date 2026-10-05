namespace BioAnalyzer.App.Infrastructure;

/// <summary>
/// Azure AD / Entra ID options for the Blazor UI and downstream Research.Api calls.
/// </summary>
public class AzureAdOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string Domain { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>
    /// Delegated scope for Research.Api, e.g. api://bioanalyzer-research-api-poc/access_as_user.
    /// </summary>
    public string ResearchApiScope { get; set; } = string.Empty;
}
