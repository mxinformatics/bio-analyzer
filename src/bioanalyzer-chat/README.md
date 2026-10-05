# BioAnalyzer Chat (agentic)

Next.js 15 app using the Vercel AI SDK and NextAuth (Azure AD). When `AGENTIC_CHAT_ENABLED=true`, `/api/chat` runs a tool-calling agent against Research API.

## Getting Started

```bash
cp .env.example .env
# fill Azure AD, Foundry, Research API base URL
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Agentic loop (Option C)

```text
askGraph (Neo4j graph-RAG)
  ├─ Answered → cited answer
  └─ miss → findLiteratureCandidates (OA PMC)
           → buildInterimAbstractBriefing (optional abstracts-only)
           → requestLiteratureIngest (Service Bus pipeline)
           → waitForIngestAndAskGraph (defer|poll)
                └─ GraphReady/Partial → askGraph again (one expansion round)
```

### Feature flags

| Variable | Default | Effect |
|----------|---------|--------|
| `AGENTIC_CHAT_ENABLED` | unset/false in prod unless set | Tool agent vs legacy `/Chat/stream` proxy |
| `EXPANSION_TOOLS_ENABLED` | true | Candidate/search tools |
| `AUTO_INGEST_ENABLED` | true | Ingest + close-loop tools |
| `INTERIM_ABSTRACTS_ENABLED` | true | Interim abstract briefing tool |
| `INGEST_WAIT_MODE` | defer | `defer` or `poll` after ingest |
| `AGENT_REQUESTER_ID` | empty | Dev override for `X-Requester-Id` (session user preferred) |
| `RESEARCH_API_KEY` | Aspire injects | Must match Research.Api `ApiAuthentication:ApiKey` (`X-Api-Key`) |

### Authentication (Entra ID)

- **UI + `/api/*`:** NextAuth Azure AD session required. Middleware protects `/api` except `/api/auth/*`; `/api/chat` returns **401** when unauthenticated.
- **Research.Api:** prefers Entra **JWT bearer** (`Authorization: Bearer`) validated against the Research API app registration. Optional `X-Api-Key` remains for local Aspire when Entra is not configured.
- **Chat → Research.Api:** NextAuth requests `RESEARCH_API_SCOPE` (terraform output `research_api_scope`) and forwards the access token.
- **Terraform:** `infrastructure/terraform/azure/bioanalyzer` creates Research API, Chat UI, Blazor UI, and daemon app registrations + Key Vault secrets.

### Prompt eval checklist

See `docs/agentic-chat-eval-checklist.md` at the repo root.
