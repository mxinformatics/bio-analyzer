# Agentic chat production defaults (Phase E)

Authoritative shared/prod settings after Phases A–D. Local Aspire may keep a documented API key for convenience; do not copy loose local flags into shared envs.

## Feature flags

| Setting | Local Aspire (suggested) | Shared / Prod |
|---------|--------------------------|---------------|
| Chat `AGENTIC_CHAT_ENABLED` | true when testing agent | **true** only after Phase E sign-off |
| Chat `EXPANSION_TOOLS_ENABLED` | true | true |
| Chat `AUTO_INGEST_ENABLED` | true only in isolated dev | **false** until E gate; then true with caps |
| Chat `INTERIM_ABSTRACTS_ENABLED` | team preference | team / compliance preference |
| Chat `INGEST_WAIT_MODE` | `defer` | **`defer`** |
| Chat `AGENT_BLOB_DOWNLOAD_TOOL_ENABLED` | false | **false** |
| `LiteratureExpansion:Enabled` | true | true |
| `LiteratureExpansion:AutoIngestEnabled` | match chat flag | match chat `AUTO_INGEST_ENABLED` |
| `LiteratureExpansion:MaxCandidates` | 3 | 3 (or lower) |
| `LiteratureExpansion:MaxIngestJobsPerRequesterPerDay` | 20 | **low double digits** (e.g. 10–20) |

## AuthN / AuthZ

| Setting | Local Aspire | Shared / Prod |
|---------|--------------|---------------|
| Research.Api `AzureAd:TenantId` | optional if API key only | **required** |
| Research.Api `ResearchApi:AzureAd:ClientId` / `Audience` | optional | **required** (resource app) |
| Research.Api `ApiAuthentication:Enabled` | true | true |
| Research.Api `ApiAuthentication:ApiKey` | optional local key | omit or rotate; prefer Entra-only |
| Research.Api `ApiAuthentication:BypassWhenApiKeyMissingInDevelopment` | true | **false** / irrelevant outside Development |
| Blazor `AzureAd:ClientId` / `ClientSecret` | UI app registration | **UI app** (must have reply URLs) |
| Chat `AZURE_AD_*` + `RESEARCH_API_SCOPE` | from terraform outputs | **required** |
| Chat session on `/api/chat` | required | **required** |

## Identity / quota / idempotency

| Setting | Local Aspire | Shared / Prod |
|---------|--------------|---------------|
| `LiteratureExpansion:RequireRequesterIdentity` | true | **true** |
| `LiteratureExpansion:AllowAnonymousRequester` | false | **false** |
| `LiteratureExpansion:RestrictJobStatusToRequester` | true | **true** |
| `LiteratureExpansion:RequireIdempotencyKey` | true | **true** |
| Client `requestedBy` / `X-Requester-Id` | ignored | **ignored** |

Requester forms: `oid:{guid}`, `app:{clientId}`, `apikey:{name}`.

## Key Vault secret ownership (critical)

| Secret | Value must be |
|--------|----------------|
| `AzureAd--ClientId` / `ClientSecret` | **bioanalyzer_ui** (reply URLs) |
| `AzureAd--TenantId` | tenant |
| `AzureAd--ResearchApiScope` | `api://bioanalyzer-research-api-{env}/access_as_user` |
| `ResearchApi--AzureAd--ClientId` | **research_api** resource app |
| `ResearchApi--AzureAd--Audience` | `api://bioanalyzer-research-api-{env}` |
| `ChatUI--AzureAd--*` | chat UI app |
| `ResearchDaemon--AzureAd--*` | daemon app for EventHandlers |

See also `infrastructure/terraform/azure/bioanalyzer/README-AUTH.md` (AADSTS500113).

## Enablement sequence

1. Deploy Research.Api + EventHandlers + App/Chat with AuthN enforced and auto-ingest **off**
2. Complete `docs/agentic-chat-hardening-verification.md` (E1–E9 + eval checklist)
3. Turn on `AUTO_INGEST_ENABLED` / `LiteratureExpansion:AutoIngestEnabled` together
4. Monitor ingest jobs, Service Bus DLQ, and Foundry cost
