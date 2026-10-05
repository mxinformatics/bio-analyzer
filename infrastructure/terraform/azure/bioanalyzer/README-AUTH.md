# BioAnalyzer Entra ID (Azure AD) app registrations

This stack provisions the Entra applications used by BioAnalyzer authentication.

## Applications

| Resource | Purpose | Reply URLs? |
|----------|---------|-------------|
| `azuread_application.research_api` | Protected API (JWT audience). Scope `access_as_user`, app role `Research.Access`. | **No** (resource app) |
| `azuread_application.bioanalyzer_ui` | Blazor BioAnalyzer UI OIDC client | **Yes** (`/signin-oidc`) |
| `azuread_application.chat_ui` | Next.js NextAuth Azure AD client | **Yes** (`/api/auth/callback/azure-ad`) |
| `azuread_application.research_daemon` | EventHandlers client credentials | **No** (daemon) |

## AADSTS500113 — No reply address is registered

This error means the **application id used in the authorize request has zero reply URLs**, not merely a mismatched URL.

Common cause after this stack: Key Vault secret `AzureAd--ClientId` was previously set to the **Research API** app id (no reply URLs). The Blazor App binds `AzureAd:ClientId` from Key Vault and then Entra correctly reports “no reply address”.

**Fix:**

| Secret | Must be |
|--------|---------|
| `AzureAd--ClientId` | **bioanalyzer_ui** client id (has reply URLs) |
| `AzureAd--ClientSecret` | **bioanalyzer_ui** client secret |
| `AzureAd--TenantId` | tenant id |
| `AzureAd--ResearchApiScope` | `api://bioanalyzer-research-api-{env}/access_as_user` |
| `ResearchApi--AzureAd--ClientId` | **research_api** client id (JWT validation only) |
| `ResearchApi--AzureAd--Audience` | `api://bioanalyzer-research-api-{env}` |

After `terraform apply`, restart the App so it reloads Key Vault. Confirm in Entra portal that the **UI** app (not Research API) lists reply URLs such as:

- `https://localhost:7103/signin-oidc`
- `http://localhost:5035/signin-oidc`
- plus any production host via `bioanalyzer_ui_redirect_uris`

## Key Vault secrets

### Interactive Blazor App (`AzureAd:*`)
- `AzureAd--TenantId`
- `AzureAd--ClientId` / `AzureAd--ClientSecret` → **UI app**
- `AzureAd--ResearchApiScope`
- Aliases: `BioAnalyzerUI--AzureAd--ClientId` / `ClientSecret`

### Research.Api resource (`ResearchApi:AzureAd:*`)
- `ResearchApi--AzureAd--ClientId`
- `ResearchApi--AzureAd--Audience`
- `AzureAd--Audience` (compat)
- `ResearchApi--Scope`

### Chat UI
- `ChatUI--AzureAd--ClientId` / `ClientSecret`

### Daemon
- `ResearchDaemon--AzureAd--ClientId` / `ClientSecret`

## Apply

```bash
cd infrastructure/terraform/azure/bioanalyzer
terraform init
terraform plan
terraform apply
```
