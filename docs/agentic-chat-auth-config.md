# Agentic chat auth & identity config matrix

| Setting | Local Aspire | Shared / Prod |
|---------|--------------|---------------|
| `AzureAd:TenantId` / `ClientId` / `Audience` | optional | **required** (Research.Api JWT) |
| `ApiAuthentication:ApiKey` | optional shared dev key | optional secondary; prefer Entra only |
| Chat `RESEARCH_API_SCOPE` | from terraform output | **required** |
| Chat session | NextAuth Azure AD | **required** |
| `LiteratureExpansion:RequireRequesterIdentity` | true | **true** |
| `LiteratureExpansion:AllowAnonymousRequester` | false | **false** |
| `LiteratureExpansion:RestrictJobStatusToRequester` | true | **true** |
| `LiteratureExpansion:RequireIdempotencyKey` | true | **true** |
| Client `requestedBy` / `X-Requester-Id` | **ignored** | **ignored** |

Requester canonical forms: `oid:{guid}`, `app:{clientId}`, `apikey:{name}`.
