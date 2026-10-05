# Agentic chat hardening verification (Phase E)

Gate before enabling shared-environment auto-ingest. Combine automated CI evidence with manual/Aspire checks.

## Automated evidence (run locally or CI)

```bash
dotnet test src/BioAnalyzer/BioAnalyzer.sln
cd src/bioanalyzer-chat && npm test
```

### Latest local run (Phase E)

| Suite | Result | Coverage highlights |
|-------|--------|---------------------|
| `BioAnalyzer.sln` unit tests | **43 passed** | Download failure path; AuthN enforce rules; requester identity; download filename allow-list; job status derivation; idempotency fingerprint |
| `bioanalyzer-chat` node tests | **2 passed** | Session gate helper (unauth rejected / auth accepted) |

CI: `.github/workflows/test-reliability-hardening.yml` runs these on PRs and pushes touching agentic-chat / Research.Api / EventHandlers paths.

## Manual / Aspire checklist (record pass/fail)

Use Aspire (`aspire start` / AppHost) with Research.Api auth enforced (API key or Entra) and chat session required.

| # | Scenario | Expect | Pass? | Notes |
|---|----------|--------|-------|-------|
| E1 | Failed download does not process | Download handler returns no `DownloadedLiterature` output; job item `Failed` @ `Download`; orchestrator never runs for that PMC | | |
| E2 | Unauth chat denied | `POST /api/chat` without session → **401** | | |
| E3 | Unauth Research.Api denied | Call ingest/chat/download without API key/JWT → **401** | | |
| E4 | Spoofed requester rejected | Body `requestedBy` / `X-Requester-Id` ignored; job `RequestedBy` = principal (`oid:` / `app:` / `apikey:`) | | |
| E5 | Invalid download filename | `GET /Literature/downloads/../etc/passwd` (or spaces/`@`) → **400**; unknown safe name not in catalog → **404** | | |
| E6 | Multi-item job status stable | Job with 2+ PMCIDs concurrent download/process → terminal `Partial` / `GraphReady` / `Failed` without lost item updates | | |
| E7 | Idempotent ingest replay | Same `Idempotency-Key` + same body → same `jobId`, `replayed=true` | | |
| E8 | Idempotency fingerprint conflict | Same key, different PMC set → **409** / conflict message | | |
| E9 | Agent blob tool gated | Default tool set has **no** `downloadFile` unless `AGENT_BLOB_DOWNLOAD_TOOL_ENABLED=true` | | |

Also run product scenarios from `docs/agentic-chat-eval-checklist.md` (§1–7) once E1–E9 pass.

## Production enablement gate

Do **not** set shared `AUTO_INGEST_ENABLED=true` / `LiteratureExpansion:AutoIngestEnabled=true` until:

1. Automated suites green in CI
2. E1–E9 manual rows filled pass
3. Production config matches `docs/agentic-chat-production-defaults.md`
4. Entra app registrations applied (`infrastructure/terraform/azure/bioanalyzer`) and Key Vault secrets correct (`AzureAd--ClientId` = **UI** app, not Research API resource)

## Sign-off

| Role | Name | Date | Result |
|------|------|------|--------|
| Engineer | | | |
| Reviewer | | | |


curl -i -X POST "http://localhost:5179/api/chat" \
  -H 'Content-Type: application/json' \
  -d '{"messages":[{"role":"user","parts":[{"type":"text","text":"hello"}]}]}'