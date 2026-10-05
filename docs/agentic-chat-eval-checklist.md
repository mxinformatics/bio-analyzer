# Agentic chat prompt eval checklist (Phase 6)

Use these scenarios against a running Research API + pipeline. Record pass/fail and notes.

## 1. In-corpus hit
- **Setup:** Question answerable from documents already in Neo4j.
- **Expect:** `askGraph` → `Answered`; cited `[Source n]` bullets; **no** candidate/ingest tools required.
- **Fail if:** Fabricated citations, skips `askGraph`, or expands corpus unnecessarily.

## 2. Out-of-corpus OA available
- **Setup:** Novel question; Entrez returns OA PMC hits not yet ingested.
- **Expect:** graph miss → candidates with `pdfLink`/`xmlLink` → optional interim abstracts (disclaimer, no `[Source n]`) → ingest jobId → wait/close-loop messaging.
- **Fail if:** Claims graph updated before `GraphReady`, uses `[Source n]` on abstracts, or multi-round expansion.

## 3. Out-of-corpus no OA
- **Setup:** Hits lack PMCID or OA links.
- **Expect:** Candidates empty/skipped reasons; honest “no ingestible OA sources”; no false download claims.
- **Fail if:** Invents PMCIDs/links or enqueues non-OA items.

## 4. Duplicate PMC
- **Setup:** Candidate already in `DownloadedLiterature`.
- **Expect:** `alreadyIngested` / ingest skip reason; no duplicate Service Bus storm.
- **Fail if:** Re-enqueues endlessly.

## 5. Graph outage
- **Setup:** graph-data-api down / circuit open.
- **Expect:** `GraphUnavailable` (or equivalent); clear error; optional expansion only if still policy-compliant.
- **Fail if:** Silent parametric hallucination presented as corpus evidence.

## 6. Idempotent ingest
- **Setup:** Replay same ingest with identical `Idempotency-Key`.
- **Expect:** Same `jobId`, `replayed=true` (or equivalent), single logical job.
- **Fail if:** Duplicate jobs for same key.

## 7. Feature flags
- `EXPANSION_TOOLS_ENABLED=false` → only `askGraph`.
- `AUTO_INGEST_ENABLED=false` → discovery/abstracts without ingest tools.
- `INTERIM_ABSTRACTS_ENABLED=false` → no interim briefing tool.

## 8. Reliability / security hardening (Phase E)

See `docs/agentic-chat-hardening-verification.md` for Critical/High gate scenarios (failed download, auth, spoofed requester, filename validation, concurrent jobs, idempotency).
