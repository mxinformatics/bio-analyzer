# Agentic Chat Implementation Review (Phases 0–6)

**Branch:** `feature/dmaxim/agentic-chat`  
**Plan:** `agentic-chat-plan.md`  
**Scope:** Verify phased Option C implementation; reliability and security analysis; prioritized refactors.  
**Date context:** Post phase-6 merge (`a4b8c85` and predecessors).

---

## Executive summary

Option C is **functionally implemented end-to-end** against the plan: structured graph results, Foundry tool-calling agent, OA candidate selection, Service Bus ingest bridge with job table updates, close-the-loop wait/re-query, interim abstracts, and phase-6 flags/caps/docs.

The design correctly keeps multi-step expansion in the Next.js agent and avoids synchronous pipeline waits in `ChatController`. Guardrails in the system prompt, feature flags, OA filtering, and idempotency scaffolding are directionally sound.

**Production readiness is not yet achieved.** The highest risks are **unauthenticated Research.Api surfaces** (including ingest and blob download), **soft/spoofable requester identity**, a **failed-download path that still emits `DownloadedLiterature`**, and **thin automated coverage** beyond candidate selection. Treat the feature as a **dev/Aspire MVP** until the P0/P1 items below are addressed.

**Overall:** Implementation matches the plan’s functional goals; reliability and AuthZ need a hardening pass before wide enablement of `AGENTIC_CHAT_ENABLED` + `AUTO_INGEST_ENABLED` in shared environments.

---

## Plan vs implementation matrix

| Phase | Plan intent | Status | Notes |
|-------|-------------|--------|-------|
| **0** Structured graph results | `ChatQueryResult` + statuses; `POST /Chat/query`, `GET /Chat/evidence`; client `askGraph` | **Done** | `AiClient.QueryStructuredAsync`; string/stream wrappers preserved. |
| **1** Tool-calling agent | `streamText` + tools; `AGENTIC_CHAT_ENABLED`; graph-first prompt | **Done** | Legacy `/Chat/stream` proxy retained. UI session gate exists; **API route is not session-gated**. |
| **2** OA candidates | `POST /Literature/candidates`; composite tool | **Done** | Caps, concurrency, `alreadyIngested`; tests cover happy/negative paths. |
| **3** Ingest bridge | SB publish from Research.Api; job table; status GET; EH job updates | **Done** | Shared-table updates from handlers. Optional per-PMC detail endpoint **not** built (plan optional). |
| **4** Close loop | `waitForIngestAndAskGraph`; poll/defer; one expansion round | **Done** | Expansion round is **prompt-enforced**, not hard-enforced in code. No server `query-after-ingest` (plan optional). |
| **5** Interim abstracts | Batch abstracts; labeled briefing; no `[Source n]` | **Done** | Default **on** (plan open-decision table said off until Phase 5; Phase 5 acceptance accepts on). |
| **6** Safety / UX / docs | Caps, AuthZ, idempotency, flags, eval, UI | **Partial** | Caps/flags/docs/checklist present. **AuthZ incomplete**; metrics beyond retrieval table **missing**; `/downloads` UI link is a **placeholder**. |

### Success criteria (plan §Success criteria)

1. In-corpus graph answers with citations — **supported** by code path.  
2. Out-of-corpus OA ingest without Blazor — **supported**.  
3. No full-text claim before ingest complete — **prompt + tool contract**; not mechanically enforced.  
4. Pipeline remains SoR — **yes**.  
5. Flags disable without breaking stream proxy — **yes** (`AGENTIC_CHAT_ENABLED`, expansion/auto-ingest filters).

---

## Architecture strengths

- Clear separation: Research.Api = contracts + enqueue; EventHandlers = durable pipeline; chat = orchestration policy.
- Structured statuses (`Answered` / `NoResults` / `InsufficientEvidence` / `GraphUnavailable`) avoid scraping SSE for tool decisions.
- Candidate path filters PMC + OA links with concurrent resolution limits.
- Ingest path: dedupe against download table, item skip reasons, job entity, optional idempotency map, hashed requester/idempotency in logs, query preview truncation.
- Close-loop defaults to `INGEST_WAIT_MODE=defer` (plan MVP recommendation); poll caps attempts/delays.
- Interim briefing carries an explicit abstract-only disclaimer and forbids `[Source n]`.
- Feature flags on both API (`AutoIngestEnabled`, expansion enabled) and chat (`EXPANSION_TOOLS_ENABLED`, `AUTO_INGEST_ENABLED`, `INTERIM_ABSTRACTS_ENABLED`).
- Graph path retains citation guardrails in `AiClient`.

---

## Reliability findings

### R1 — Critical: failed download still completes and publishes “success” downstream

**Where:** `DownloadRequestHandler.Run`  
**Behavior:** On download failure it marks the ingest item `Failed`, then **always** `CompleteMessageAsync` and **returns** a `DownloadedLiterature` Service Bus output (with possibly empty/unresolved content).

**Impact:** Downstream `DocumentProcessingOrchestratorHandler` can still run chunk/embed/graph on missing or empty blobs, produce confusing failures, or waste compute. Job status may flip Failed → Processing → Failed again. Message is not abandoned/dead-lettered on download failure, so there is no automatic retry of the download itself.

**Recommendation:** Only emit `DownloadedLiterature` / complete the trigger when `downloadSucceeded`. On failure: update job, dead-letter or complete **without** output (or emit a dedicated failure message), and do not start processing.

### R2 — High: ingest job status races and lost updates

**Where:** `IngestJobStatusClient.MarkItemProgressAsync`  
**Behavior:** Read-modify-write upsert of `ItemsJson` with no ETag/conditional update. Concurrent PMC items in one job (or overlapping handlers) can overwrite each other. Exceptions are swallowed (warn log only).

**Impact:** Stuck `Accepted`/`Downloading`, incorrect `Partial`/`GraphReady`, close-loop never re-queries or re-queries too early.

**Recommendation:** Use Azure Table ETag optimistic concurrency with retry; or per-item rows + job rollup; surface permanent write failures to metrics/alerts.

### R3 — High: Service Bus batch publish is all-or-nothing and non-transactional with job write

**Where:** `EventBusClient.PublishLiteratureDownloadRequestsAsync`, `LiteratureIngestService.RequestIngestAsync`  
**Behavior:** Single batch; if one message does not fit, entire publish throws. Job row is written **after** publish. Order is: publish bus → upsert job → upsert idempotency. If process dies after publish and before job upsert, messages process with `JobId` but **no job row** (status client no-ops). If job writes succeed but client never sees response, client may retry; without required idempotency this creates **duplicate jobs/messages**.

**Recommendation:** Write job as `Accepted` first (or outbox pattern); require idempotency in non-dev; split oversized batches; consider publish after durable job+outbox commit.

### R4 — Medium: quota check is full-table scan and bypassable

**Where:** `LiteratureIngestService.EnsureDailyQuotaAsync`  
**Behavior:** `GetAllAsync` on ingest job table; filters in memory. Quota skipped when `RequestedBy` is empty. Default config allows anonymous requesters.

**Impact:** Latency/cost growth with table size; easy bypass of daily caps; weak DoS/cost control on auto-ingest.

**Recommendation:** Partition by day+requester or maintain counter entities; **require** requester identity when quota > 0; fail closed if identity missing in production.

### R5 — Medium: dual PMC Open Data resolution

**Where:** Research.Api `NcbiClient` / search service vs EventHandlers `DownloadRequestHandler.ResolvePmcOpenDataLinks`  
**Plan risk table** called this out.

**Impact:** Candidate shows a link; handler re-resolves and may disagree (version selection, PDF vs XML preference). Agent-supplied `downloadLink` is largely ignored by the handler (handler always re-resolves from PMCID)—good for SSRF mitigation on the worker side, but agent/API “selected link” is not the download source of truth.

**Recommendation:** Shared library or contract tests for PMCID → HTTPS URL; document that handler is SoR for bytes.

### R6 — Medium: close-loop and expansion policy are soft

**Where:** `agentPrompt.ts`, `ingestCloseLoop.ts`, `route.ts` (`stepCountIs(12)`)  
**Behavior:** Single expansion round is instructed, not locked (no server-side session state). Model can call `findLiteratureCandidates` / `requestLiteratureIngest` again, or spam granular tools. `waitMode` tool arg can override env to `poll` and hold the request up to ~minute-scale (bounded, but still).

**Impact:** Extra NCBI/Foundry/SB cost; longer chat requests; possible multi-ingest storms under prompt drift.

**Recommendation:** Track `expansionRound` in tool closure / request-scoped state and refuse second ingest; ignore or clamp client `waitMode`; lower `stepCountIs` when auto-ingest off.

### R7 — Medium: GraphReady vs graph visibility lag

**Where:** Orchestrator marks `GraphReady` immediately after `BuildGraphAsync` returns.  
**Impact:** Close-loop `askGraph` may still miss new chunks if indexing/consistency lags (or enrich failed but graph write succeeded partially).

**Recommendation:** Short post-ready delay or “graph searchable” probe; treat first post-ingest miss as retryable without new expansion round.

### R8 — Low/Medium: logging and PII

**Where:** `AiClient` logs full query text; chat tools `console.log` full queries; Research.Api ingest hashes requester but chat may log freely.  
**Phase 6** asked for query-hash metrics and limited PHI-like storage.

**Recommendation:** Prefer length + hash in logs; keep `QueryPreview` truncated (already); avoid logging full abstracts in production.

### R9 — Low: SSE done event escaping

**Where:** `ChatController.StreamQuery` writes `event: done\\ndata: [DONE]\\n\\n` (literal backslashes in places).  
**Impact:** Legacy stream proxy may not see a clean done event (usually ends on stream close). Agentic path does not use this.

### R10 — Test coverage gaps

**Present:** `LiteratureExpansionServiceTests` (candidate selection).  
**Missing / thin:** `LiteratureIngestService` (quota, idempotency, dedupe, disabled flag); EventHandlers job derivation; download failure should not emit processing; chat tool flag matrix; close-loop poll/defer; AuthZ restrict status; E2E Aspire path from plan test matrix.

---

## Security findings

### S1 — Critical: Research.Api controllers effectively open

**Where:** `Program.cs` calls `UseAuthentication` / `UseAuthorization` but **no** `AddAuthentication` scheme registration in `ResearchApiDependencies`, and controllers lack `[Authorize]`.  
**Impact:** Anyone who can reach Research.Api can:

- Run graph/OpenAI-backed chat queries (`/Chat/*`)
- Search NCBI and resolve OA links
- **Enqueue pipeline work** (`POST /Literature/ingest`) → Service Bus → blob + chunk/embed/graph **cost and corpus pollution**
- Read ingest jobs and download list
- **Download blobs** via `GET /Literature/downloads/{fileName}`
- **Upload** manual literature (`POST /Literature/uploads/manual`, 100MB)

Network isolation (Aspire/private VNet) may mitigate in some deploys; it is not an application control.

**Recommendation:** Service-to-service auth (managed identity / API key / JWT) on Research.Api; least-privilege for chat BFF; never expose ingest/upload/download anonymously on a public URL.

### S2 — Critical: Next.js `/api/chat` is not authenticated

**Where:** `app/page.tsx` redirects unauthenticated users to Azure AD; `app/api/chat/route.ts` has **no** `getServerSession` / token check. No `middleware.ts` found.  
**Impact:** Unauthenticated clients can invoke the agent (Foundry tokens + Research.Api side effects) if the chat app is reachable.

**Recommendation:** Require session on `/api/chat` (and other API routes); return 401 otherwise. Propagate user id to Research.Api.

### S3 — High: spoofable `X-Requester-Id` / soft AuthZ

**Where:** Chat uses `AGENT_REQUESTER_ID` env (static); API accepts body `requestedBy` or header. `RestrictJobStatusToRequester` defaults **false**. When true, comparison is plain string equality on attacker-controlled header.  
**Impact:** Quota attribution is fakeable; job status is world-readable by default; “restriction” is not real AuthZ.

**Recommendation:** Derive requester from verified session/JWT only; strip client-supplied `requestedBy` on public edges; enable restriction with signed identity; do not trust headers alone.

### S4 — High: blob download path / file name trust

**Where:** `LiteratureController.DownloadFile` → `StorageClient.DownloadFileAsync(fileName)` with no obvious canonicalization allow-list. Chat tool `downloadFile` exposes this.  
**Impact:** Depending on blob SDK key handling, risk of unauthorized object read or path-style abuse; information disclosure of stored literature.

**Recommendation:** Allow only safe filenames (`[A-Za-z0-9._-]+`), resolve against download table row keys, authz per caller; remove `downloadFile` from agent tools unless necessary.

### S5 — Medium: agent-supplied download URLs (SSRF / confusion)

**Where:** `LiteratureIngestService` accepts `downloadLink`/`xmlLink` from the client and will enqueue them after a weak `http(s)` prefix check (no host allow-list). EventHandlers **re-resolve from PMCID** (good). Residual risk if any consumer fetches the bus `DownloadLink` as-is in the future, or if link fields are shown/trusted elsewhere.

**Recommendation:** Allow-list hosts (`pmc-oa-opendata.s3.amazonaws.com`, known PMC HTTPS hosts); or drop client links and always resolve server-side by PMCID before enqueue.

### S6 — Medium: idempotency defaults off; key not bound to body

**Where:** `RequireIdempotencyKey: false`; chat always sends a **new** UUID per call. Idempotency map stores key → jobId without hash of request body.  
**Impact:** Retries create duplicate jobs; same key with different PMC sets returns original job (replay) without conflict detection.

**Recommendation:** Require key in prod; chat should stable-hash `(user, query, sorted pmcIds)` for a turn; store request fingerprint; 409 on fingerprint mismatch.

### S7 — Medium: prompt injection via tool/corpus content

Abstracts, titles, and graph snippets flow into the model. System prompt is strong but not a sandbox. Malicious paper text could steer tool use (extra ingest, skip disclaimer).

**Recommendation:** Keep tool surface minimal in prod (`EXPANSION_TOOLS_ENABLED` / hide granular tools); server-side enforce max items and OA policy regardless of model; sanitize/length-limit tool outputs fed back to the model.

### S8 — Low: `NODE_TLS_REJECT_UNAUTHORIZED=0` in `.env.example`

Encourages disabling TLS verification. Remove from example or document as local-only hazard.

### S9 — Low: package / dependency noise

Prior build noted `Microsoft.OpenApi` NU1903. Track and patch independently of agentic chat.

### S10 — Informational: interim default on

Abstracts are third-party content shown to users quickly. Disclaimer is good; still a content/compliance surface (copyright, medical interpretation). Keep flag easy to disable.

---

## Severity-ordered proposed changes

### P0 — Fix before enabling auto-ingest outside a trusted network

1. **Download handler:** do not publish `DownloadedLiterature` or complete-as-success when download fails; align job terminal state once.  
2. **Authenticate Research.Api** (at least ingest, upload, downloads, chat query) and **`/api/chat`**.  
3. **Bind requester to verified identity**; default deny anonymous ingest when `MaxIngestJobsPerRequesterPerDay > 0`.  
4. **Harden `DownloadFile`** filename validation + authz; drop or gate `downloadFile` tool.

### P1 — Reliability and abuse resistance

5. **Optimistic concurrency** (or per-item rows) for ingest job updates.  
6. **Durable outbox / job-first** publish; **require idempotency** in shared envs; stable keys from chat.  
7. **Host allow-list** for any URL accepted on ingest; treat handler PMC resolve as SoR.  
8. **Hard cap one expansion ingest per chat request** in tool layer (not only prompt).  
9. **Quota without full table scan**; fail closed without requester in prod.  
10. **Enable `RestrictJobStatusToRequester`** only with real identity (see P0).

### P2 — Product polish and observability

11. Replace `/downloads` placeholder with real route or Blazor/App deep link, or remove the affordance.  
12. Metrics: expansion rate, enqueue success/fail, time-to-GraphReady, post-ingest `askGraph` success, tool errors (plan Phase 6).  
13. Shared PMC Open Data resolution library + contract tests.  
14. Reduce query/abstract logging; hash where possible.  
15. Fix legacy SSE done payload if stream proxy remains supported.  
16. Consider short GraphReady → searchable delay or retry guidance in close-loop.

### P3 — Tests and docs

17. Unit tests: `LiteratureIngestService` (flags, quota, idempotency, dedupe, link resolve failure).  
18. Handler tests: failure does not emit processing message; job status derivation Partial/GraphReady/Failed.  
19. Chat tests: flag matrix tool sets; close-loop defer vs poll; session 401.  
20. Manual/E2E: execute `docs/agentic-chat-eval-checklist.md` against Aspire and record results.  
21. Document production config matrix (flags, `RequireIdempotencyKey`, `RestrictJobStatusToRequester`, AuthN).

---

## Suggested production defaults (when shipping)

| Setting | Suggested prod value |
|---------|----------------------|
| `AGENTIC_CHAT_ENABLED` | on only after P0 |
| `EXPANSION_TOOLS_ENABLED` | on |
| `AUTO_INGEST_ENABLED` | **off** until P0 download+auth fixed; then on with caps |
| `INTERIM_ABSTRACTS_ENABLED` | team preference (compliance) |
| `INGEST_WAIT_MODE` | `defer` |
| `LiteratureExpansion:AutoIngestEnabled` | match chat flag |
| `MaxIngestJobsPerRequesterPerDay` | low double digits |
| `RequireIdempotencyKey` | **true** |
| `RestrictJobStatusToRequester` | **true** with real identity |
| Granular tools (`searchLiterature`, `downloadFile`, …) | hide in prod agent set |

---

## File reference map (review anchors)

| Area | Paths |
|------|--------|
| Plan / eval | `agentic-chat-plan.md`, `docs/agentic-chat-eval-checklist.md` |
| Chat route / tools | `src/bioanalyzer-chat/app/api/chat/route.ts`, `lib/ResearchTools.ts`, `lib/ResearchApi.ts`, `lib/agentPrompt.ts`, `lib/ingestCloseLoop.ts`, `lib/interimAbstracts.ts`, `lib/agentFeatureFlags.ts` |
| UI | `src/bioanalyzer-chat/components/chat/ChatList.tsx`, `app/page.tsx` |
| Chat API | `ChatController.cs`, `AiClient.cs` |
| Literature | `LiteratureController.cs`, `LiteratureExpansionService.cs`, `LiteratureIngestService.cs`, `EventBusClient.cs`, `LiteratureExpansionConfiguration` / `appsettings.json` |
| Pipeline | `DownloadRequestHandler.cs`, `DocumentProcessingOrchestratorHandler.cs`, `IngestJobStatusClient.cs` |
| Tests | `BioAnalyzer.Research.Api.Tests/LiteratureExpansionServiceTests.cs` |

---

## Conclusion

Phases 0–6 deliver a coherent Option C agent loop that matches the plan’s architecture and most acceptance intents. The remaining work is less “feature complete” and more **make the side effects safe and the status plane trustworthy**: authenticate the BFF and API, stop failed downloads from looking like success, tighten identity/quota/idempotency, and extend tests beyond candidate selection.

**Do not implement the refactors in this document unless explicitly requested;** use this list to prioritize a hardening phase (recommended: P0 then P1 before broad `AUTO_INGEST_ENABLED`).
