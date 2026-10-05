# Agentic Chat Expansion Plan (Option C)

## Goal
Enable the **bioanalyzer-chat** agent to answer biological research questions by:
1. Querying the existing Neo4j graph-RAG path first.
2. When graph evidence is insufficient, using tools to search literature, select open-access candidates, request ingest into the document pipeline, and either wait/poll or guide the user to retry once sources are graph-ready.

Option C keeps multi-step reasoning in the **Next.js agent** and avoids making `ChatController` / `AiClient` synchronously download and process papers in one HTTP request.

## Current state (baseline)

### Chat path today
- `bioanalyzer-chat/app/api/chat/route.ts` proxies the latest user text to Research API `GET /Chat/stream` and adapts SSE into the Vercel AI SDK UI stream.
- Research API `ChatController` → `AIQueryService` → `AiClient`:
  - Calls `IGraphQueryClient` (`graph-data-api` `POST /query`).
  - Filters by `GraphEvidenceMinScore`, `GraphMinEvidenceCount`, `GraphMaxEvidenceCount`.
  - On empty/insufficient evidence returns fixed text: *"I don't know based on the currently available graph evidence for that question."*
  - Otherwise answers from graph snippets only via Azure OpenAI with citation guardrails.
- **No automatic literature search or pipeline enqueue** on chat miss.

### Literature / pipeline path today
```
Search (Entrez) → download link (PMC AWS) → Service Bus DownloadDocumentQueue
  → DownloadRequestHandler (blob)
  → DocumentProcessingRequestedQueue
  → Chunk → Embed → Graph (+ optional enrich)
  → DocumentGraphReady | DocumentProcessingFailed
```
- Blazor App publishes download/processing via `EventBusClient`.
- Research API exposes literature HTTP APIs but **does not publish Service Bus messages**.
- `DocumentGraphReady` is emitted; **no chat consumer** re-answers the original question.

### Agent tools today (`lib/ResearchTools.ts` + `lib/ResearchApi.ts`)
| Tool | Backend | Gap vs Option C |
|------|---------|-----------------|
| `searchLiterature` | `GET /Literature` | Exists; returns Entrez search IDs/count, not full ranked OA candidates |
| `getLiteratureSummary` | `GET /Literature/summary` | Exists; need PMCID filtering |
| `getLiteratureAbstract` | `GET /Literature/abstract` | Exists; interim context only |
| `getDownloadLink` | `GET /Literature/download` | Exists; **link only**, does not ingest |
| `downloadFile` | `GET /Literature/downloads/{file}` | Blob download for already-stored files |
| *(none)* | graph Q&A | Chat route bypasses tools entirely |
| *(none)* | enqueue ingest | No API |
| *(none)* | processing status / graph-ready | No chat-facing job API |

### Design constraints
- Full chunk/embed/graph is **async (minutes)** — agent must not block a single tool call on end-to-end pipeline completion without an explicit job/poll model.
- Only **PMC OA** (AWS `pmc-oa-opendata`) content is reliably ingestible via current download handlers.
- Entrez hits often lack PMCID or OA full text — candidate selection must filter aggressively.
- Citation quality should remain graph-backed for final scientific answers when possible; abstracts are interim only.
- Deduplicate by PMCID against existing downloads / in-flight processing / graph docs.

---

## Target agent loop (Option C)

```text
User question
  → tool: askGraph (or queryGraphEvidence)
       ├─ status=Answered → stream/final answer + sources
       └─ status=InsufficientEvidence | NoResults
            → tool: searchLiteratureCandidates (search + summary + OA filter)
            → (optional) tool: getLiteratureAbstract for top titles (interim answer)
            → tool: requestLiteratureIngest (enqueue N PMCIDs)
            → tool: getIngestJobStatus / wait policy
                 ├─ Complete → tool: askGraph again
                 └─ Pending/Failed → tell user status + what was queued
```

System prompt / tool descriptions encode this policy so the model does not invent papers or skip graph-first retrieval.

---

## Phased plan

### Phase 0 — Contract and observability foundation
**Objective:** Make graph miss outcomes machine-readable for tools (without changing user-facing Blazor chat behavior much).

#### Research.Api
1. Introduce a structured chat/graph result DTO, e.g. `GraphChatResult` / `ChatQueryResult`:
   - `status`: `Answered` | `NoResults` | `InsufficientEvidence` | `GraphUnavailable`
   - `answer` (string; may be empty when not answered)
   - `retrieval`: `totalResults`, `selectedResults`, `min/avg/maxScore`, `threshold`, `topK`
   - `sources[]`: documentName, pmcId/sourceSystemDocId, page, chunk, score (when answered)
   - `query` echo / `queryHash` (align with existing retrieval metrics)
2. Extend `IAiClient` / `AiClient` to return this structure from a new method (keep existing `QueryAsync`/`StreamQueryAsync` wrappers for backward compatibility, or map stream to text-only).
3. Add `GET /Chat/evidence` or `POST /Chat/query` returning JSON `ChatQueryResult` (preferred for tools over scraping SSE text).
4. Ensure retrieval metric `Status` values remain stable and match tool-visible statuses.

#### bioanalyzer-chat
5. Add `ResearchApi.queryGraph` / `askGraph` client method calling the new JSON endpoint.
6. Do **not** yet switch `/api/chat` off the stream proxy (optional later).

#### Acceptance
- Tool can distinguish “model doesn’t know” vs transport failure vs true empty graph.
- Existing `/Chat` and `/Chat/stream` still work for Blazor / current Next proxy.

---

### Phase 1 — Wire real agent tool-calling chat (replace pure stream proxy)
**Objective:** `/api/chat` becomes an agent that can call tools, with **graph-first** behavior.

#### bioanalyzer-chat
1. Refactor `app/api/chat/route.ts` to use Vercel AI SDK `streamText` (or equivalent) with:
   - Azure OpenAI / Foundry model config (env already partially present per README).
   - `tools: getResearchTools(researchApi)`.
   - System instructions:
     - Always call `askGraph` first for scientific questions grounded in the corpus.
     - If insufficient, follow expansion tools (Phase 2+).
     - Never fabricate citations; prefer graph sources.
2. Restore/enable `getResearchTools` usage (currently defined but unused by `route.ts`).
3. UI: ensure tool call parts render usefully (status: searching graph, searching literature, etc.) — minimal viable display.
4. Feature flag: `AGENTIC_CHAT_ENABLED` (fallback to current `/Chat/stream` proxy when off).

#### Research.Api
5. Harden CORS/auth assumptions for server-side Next calls if needed (server-to-server; no browser CORS required for route handler).

#### Acceptance
- Asking a question in-corpus returns a cited graph answer via tools.
- Asking an out-of-corpus question returns structured insufficient status (not only opaque prose), and agent can explain that corpus lacks evidence **without** yet auto-ingesting.

---

### Phase 2 — Literature candidate selection tools
**Objective:** High-quality, OA-ingestible candidate lists for the agent.

#### Research.Api
1. Add orchestration endpoint, e.g. `POST /Literature/candidates`:
   - Input: `query`, `maxCandidates` (default 3), `requirePmcId` (true), `requireOpenAccessLink` (true).
   - Steps:
     1. `LiteratureSearchAsync` (Entrez).
     2. `GetLiteratureSummaries` for returned UIDs.
     3. Filter to rows with non-empty `PmcId`.
     4. For each PMCID (capped concurrency), call existing download-link resolution (`GetLiteratureDownloadLinkAsync`).
     5. Keep candidates with non-empty `PdfLink` and/or XML/`ArchiveLink`.
     6. Optional: exclude PMCIDs already present in download table / known graph docs (storage lookup).
   - Output: ranked list `{ pmid, pmcId, title, doi, pdfLink, xmlLink, alreadyIngested }`.
2. Reuse NCBI rate coordination (`NcbiRequestCoordinator`); add timeouts and partial-success semantics.
3. Config section `LiteratureExpansion`: max candidates, max search retmax, enable flag.

#### bioanalyzer-chat
4. `ResearchApi.findLiteratureCandidates(...)`.
5. Tool `findLiteratureCandidates` with clear description: use only after graph miss; prefer OA PMC full text.
6. Keep granular tools (`searchLiterature`, `getLiteratureSummary`, `getDownloadLink`) for debugging but steer policy to the composite tool.

#### Acceptance
- For a known OA topic, tool returns ≤N candidates with PMCID + HTTPS PDF/XML links.
- Non-OA-only hits are excluded.
- Already-downloaded PMCIDs marked, not blindly re-queued later.

---

### Phase 3 — Ingest request API (bridge agent → existing pipeline)
**Objective:** Agent can start the same pipeline Blazor uses today.

#### Research.Api (recommended owner)
1. Add Service Bus publishing capability (mirror App `EventConfiguration` / `EventBusClient`):
   - `LiteratureDownloadTopic` / queue used by `DownloadRequestHandler`.
   - Optional direct `DocumentProcessingRequestedQueue` only if blob already exists.
2. Add `POST /Literature/ingest`:
   - Body: `{ items: [{ pmcId, title, doi, downloadLink? }], correlationId?, requestedBy? }`.
   - Server resolves download links if missing.
   - Dedupes in-flight/completed downloads.
   - Publishes `LiteratureDownloadRequest` messages.
   - Returns `{ jobId, enqueued: [...], skipped: [...], status: Accepted }`.
3. Persist **ingest job** entity in Azure Table (new table or extend processing status):
   - `JobId`, `CorrelationId`, `QueryPreview`, `PmcIds[]`, `Status` (`Accepted`|`Downloading`|`Processing`|`GraphReady`|`Failed`|`Partial`), timestamps, errors.
4. Add `GET /Literature/ingest/{jobId}` for status.
5. Optional: `GET /Literature/ingest/{jobId}/pmc/{pmcId}` detailed stage from `DocumentProcessingStatus`.

#### EventHandlers
6. Ensure download → processing chain remains reliable when messages originate from Research.Api (same contracts as App).
7. On `DocumentGraphReady` / `DocumentProcessingFailed`, update ingest job rows (new small function **or** extend orchestrator to call Research.Api callback / write shared table directly).
   - Prefer **shared table writes from EventHandlers** to avoid Research.Api needing a public webhook initially.

#### bioanalyzer-chat
8. Tools:
   - `requestLiteratureIngest`
   - `getIngestJobStatus`
9. Agent policy: enqueue at most N (config); tell user which PMCIDs were queued; do not claim graph updated until status says so.

#### Acceptance
- Tool enqueue results in blob download + chunk/embed/graph for a test PMCID.
- Job status progresses to `GraphReady` (or `Failed` with stage).
- Duplicate enqueue is skipped or no-ops safely.

---

### Phase 4 — Close the loop (re-query graph after ingest)
**Objective:** Agent can finish the scientific answer after sources land in Neo4j.

#### bioanalyzer-chat
1. Tool policy after `requestLiteratureIngest`:
   - **Short poll** (e.g. 3–5 attempts, 5–15s backoff) via `getIngestJobStatus` for interactive UX, **or**
   - Return immediately with “sources are processing; ask again in a few minutes” if job still pending (configurable `INGEST_WAIT_MODE=poll|defer`).
2. When job `GraphReady` or enough PMCIDs ready: call `askGraph` again with the **original user question**.
3. If still insufficient after ingest: report remaining gap; do not infinite-loop (max expansion rounds = 1 in v1).

#### Research.Api
4. Optional helper `POST /Chat/query-after-ingest` with `{ query, jobId }` that waits server-side up to a cap — **only if** poll logic should leave the browser agent; default keep wait in Next tool layer to avoid Functions/API timeouts.

#### Acceptance
- End-to-end: out-of-corpus question → candidates → ingest → graph ready → cited answer from new docs.
- Single expansion round max; clear messaging on timeout/failure.

---

### Phase 5 — Interim abstract answers (optional quality-of-life)
**Objective:** Reduce “dead air” while pipeline runs (light Option B inside Option C).

1. After candidates selected, agent may call `getLiteratureAbstract` (or batch endpoint) for top 1–2 OA works.
2. Produce an **explicitly labeled interim** answer (“Based on abstracts only; full-text graph ingest in progress…”).
3. System prompt forbids presenting abstract-only answers as graph-cited corpus evidence.
4. When graph re-query succeeds, replace/supplement with full cited answer.

#### Acceptance
- User sees interim abstract insight + ingest status without false `[Source n]` graph citations.

---

### Phase 6 — Safety, limits, eval, and UX polish
**Objective:** Production readiness.

1. **Caps:** max candidates/question, max concurrent ingest jobs/user/day, OA-only default, reject missing PMCID.
2. **AuthZ:** if chat is authenticated (NextAuth/Azure AD), plumb identity into ingest job `requestedBy`; restrict status reads.
3. **PII/logging:** continue query hash metrics; avoid storing full PHI-like prompts if applicable.
4. **Idempotency keys** on ingest POST.
5. **Metrics:** expansion trigger rate, enqueue success, time-to-graph-ready, post-ingest answer success, tool error rates.
6. **UI:** tool traces collapsed by default; show PMC titles queued; link to downloads page if applicable.
7. **Prompt eval set:** in-corpus hit, out-of-corpus OA available, out-of-corpus no OA, duplicate PMC, graph outage.
8. **Feature flags:** `ExpansionToolsEnabled`, `AutoIngestEnabled` (search-only vs search+ingest), `InterimAbstractsEnabled`.
9. Documentation: update `CLAUDE.md` / chat README with agent loop and env vars.

---

## Suggested implementation order and ownership

| Phase | Primary surfaces | Depends on |
|-------|------------------|------------|
| 0 | Research.Api `AiClient`, Chat DTOs | — |
| 1 | `bioanalyzer-chat` route + tools | Phase 0 |
| 2 | Research.Api literature orchestration + tools | Phase 1 |
| 3 | Research.Api SB publish + job table; EventHandlers job updates | Phase 2 |
| 4 | Agent close-the-loop policy | Phase 3 |
| 5 | Optional abstracts | Phase 2+ |
| 6 | Hardening | Phases 0–4 |

**Recommended MVP ship point:** end of **Phase 4** with `INGEST_WAIT_MODE=defer` (user retries) if poll-in-tool is flaky; upgrade to short poll next.

---

## Key code touchpoints

### bioanalyzer-chat
- `app/api/chat/route.ts` — replace pure proxy with tool-calling agent (flagged).
- `lib/ResearchApi.ts` — `queryGraph`, `findLiteratureCandidates`, `requestIngest`, `getIngestJobStatus`.
- `lib/ResearchTools.ts` — new tools + stricter descriptions; deprecate misuse of link-only download as “ingested”.
- System prompt module (new), e.g. `lib/agentPrompt.ts`.
- Env: model endpoint, `AGENTIC_CHAT_ENABLED`, wait mode, research API base (existing Aspire `services__researchApi__*`).

### BioAnalyzer.Research.Api
- `Domain/Clients/AiClient.cs` — structured result.
- `Controllers/ChatController.cs` — JSON query endpoint.
- `Controllers/LiteratureController.cs` — `candidates`, `ingest`, ingest status.
- New: `LiteratureExpansionService` / `IngestJobService`.
- New: Service Bus sender registration in `ResearchApiDependencies`.
- Config: `EventConfiguration` (or shared), `LiteratureExpansion` options.
- Storage: ingest job table via existing Azure table abstractions.

### BioAnalyzer.EventHandlers
- `DocumentProcessingOrchestratorHandler` outputs already define `DocumentGraphReady`.
- New/extended handler or shared status writer to mark ingest jobs complete/failed.
- Confirm message shapes match Research.Api publishers (`DownloadRequest` / `LiteratureDownloadRequest`).

### Out of scope for Option C core
- Making Blazor Chat fully agentic (can later call same Research.Api APIs).
- Synchronous in-request chunk/embed/graph inside `AiClient` (Option D).
- Changing Neo4j schema or graph-data-api ranking (unless candidate exclusion needs a “has PMC” check API).

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Agent skips graph and answers from parametric memory | Hard system rule + require `askGraph` first; low temperature |
| Non-OA papers queued and fail download | Phase 2 OA link filter before enqueue |
| Long pipeline vs chat timeout | Job model + defer/poll; never block on full pipeline in one tool without deadline |
| Duplicate pipeline load | Dedupe by PMCID + job/download table |
| NCBI rate limits | Existing coordinator; batch summaries; limit candidates |
| Dual download-link logic (Api vs EventHandlers) | Prefer single resolution service or shared contract tests |
| Cost of extra LLM + embeddings | Caps; expansion only on graph miss; max one round |

---

## Test plan (per phase)

### Phase 0–1
- Unit/integration: `AiClient` statuses for empty, below-threshold, success.
- Manual: agent calls `askGraph` and surfaces insufficient status.

### Phase 2
- Integration: candidates for a known OA PMC topic include that PMCID with HTTPS links.
- Negative: non-OA filtered out.

### Phase 3–4
- E2E local Aspire: tool ingest → functions → graph nodes → second `askGraph` cites new doc.
- Failure path: invalid PMCID → job `Failed` with stage.

### Phase 5–6
- Prompt eval checklist; load/limit tests; authz on job status.

---

## Open decisions (defaults for implementation)

| Decision | Default |
|----------|---------|
| Where to publish Service Bus | **Research.Api** (agent calls one backend) |
| Max docs per expansion | **2** |
| Wait mode MVP | **defer** (status + ask again); add short poll behind flag |
| Interim abstracts | **off** until Phase 5 |
| Blazor chat | unchanged initially |
| Auto-ingest vs suggest-only | **auto-ingest** after OA filter, with user-visible PMC list |

---

## Success criteria
1. In-corpus questions still answered from Neo4j with citations.
2. Out-of-corpus questions trigger tool-based search and OA ingest without manual Blazor download.
3. Agent never claims full-text graph evidence before ingest completion.
4. Pipeline remains the system of record for durable corpus growth (chunk → embed → graph).
5. Feature can be disabled via flags without breaking current `/Chat/stream` proxy behavior.
