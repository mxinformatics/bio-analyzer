# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

### .NET (requires .NET 10 SDK — see `src/BioAnalyzer/global.json`)

```bash
# Build full solution
dotnet build src/BioAnalyzer/BioAnalyzer.sln

# Run all services locally via .NET Aspire orchestrator (recommended for local dev)
dotnet run --project src/BioAnalyzer/BioAnalyzer.AppHost

# Run individual services
dotnet run --project src/BioAnalyzer/BioAnalyzer.Research.Api
dotnet run --project src/BioAnalyzer/BioAnalyzer.App
```

Test projects:
- `src/BioAnalyzer/BioAnalyzer.Research.Api.Tests` (xUnit) — literature expansion/candidate selection.

```bash
dotnet test src/BioAnalyzer/BioAnalyzer.Research.Api.Tests/BioAnalyzer.Research.Api.Tests.csproj
```

### Next.js chat frontend (`src/bioanalyzer-chat`)

```bash
cd src/bioanalyzer-chat
npm install
npm run dev        # Turbopack dev server on port 3000
npm run build      # Production build
npm run lint       # ESLint
```

Copy `.env.example` to `.env` and fill in Azure AD, Azure OpenAI, and NextAuth values before running.

### Helm chart generation

```bash
cd src/BioAnalyzer/BioAnalyzer.AppHost
aspirate generate --skip-build --output-format helm
```

### Docker images

Each deployable service has its own `Dockerfile`. CI workflows (`.github/workflows/`) build and push to Docker Hub on pushes to `main`, scoped to path changes per service.

## Architecture

BioAnalyzer is a biological literature research platform with AI-assisted Q&A. It has four deployable .NET services, one Next.js frontend, and shared libraries.

### Components

**BioAnalyzer.AppHost** — .NET Aspire orchestrator. Wires together all services for local development with service discovery. Not deployed; used only for local runs.

**BioAnalyzer.Research.Api** — ASP.NET Core Web API; the central backend. Two controller areas:
- `LiteratureController`: Literature discovery via NCBI/Entrez (search, summaries, abstracts) and PMC Open Data download links. Also serves stored PDFs from Azure Blob Storage.
- `ChatController`: Graph-RAG Q&A endpoint. `AiClient` queries an external graph data API (vector similarity search), scores/filters evidence nodes, then calls Azure OpenAI to produce cited responses. Exposes both a synchronous `GET /Chat` and an SSE streaming endpoint `GET /Chat/stream`.

**BioAnalyzer.App** — Blazor Server UI for literature search and download. Calls Research API via `ResearchApiClient` and publishes download-request events to Azure Service Bus via `EventBusClient`.

**BioAnalyzer.EventHandlers** — Azure Functions (isolated worker) processing three Service Bus queues:
- `DownloadRequestHandler` (`DownloadDocumentQueue`): Resolves PMC Open Data S3 metadata to get direct PDF/XML links, downloads the PDF, stores it in Azure Blob Storage, then publishes a `DownloadedLiterature` message to `DocumentDownloadedTopic`.
- `BuildDocumentListHandler` (`BuildDocumentListQueue`): Persists downloaded literature metadata to Azure Table Storage.
- `DocumentProcessingOrchestratorHandler` (`DocumentProcessingRequestedQueue`): Orchestrates a Chunk → Embed → Graph pipeline by calling three external HTTP APIs in sequence. Writes processing status records to Azure Table Storage at each stage. Emits `DocumentGraphReady` or `DocumentProcessingFailed` Service Bus messages.

**bioanalyzer-chat** — Next.js 15 app using the Vercel AI SDK + NextAuth (Azure AD). When `AGENTIC_CHAT_ENABLED=true`, `/api/chat` runs a Foundry tool-calling agent (`askGraph` → OA candidates → optional interim abstracts → ingest → close-the-loop re-query). Flags: `EXPANSION_TOOLS_ENABLED`, `AUTO_INGEST_ENABLED`, `INTERIM_ABSTRACTS_ENABLED`, `INGEST_WAIT_MODE`. See `src/bioanalyzer-chat/README.md` and `docs/agentic-chat-eval-checklist.md`. Legacy mode proxies Research API `GET /Chat/stream`.

### Shared libraries

**BioAnalyzer.AzureStorage / BioAnalyzer.AzureStorage.Contracts** — Abstraction over Azure Blob Storage (`IBlobContext` / `AzureBlobContext`) and Azure Table Storage (`ITableContext` / `AzureTableContext`). Consumed by both Research API and EventHandlers. Storage contexts are created per-scope via factory lambdas in DI registration, not as typed singletons.

**BioAnalyzer.Infrastructure** — Cross-cutting: `SecureConfiguration` (controls Azure Key Vault loading), `IAzureCredentialFactory` (applies `ManagedIdentityClientId` when populated, falls back to `DefaultAzureCredential`).

**BioAnalyzer.ServiceDefaults** — .NET Aspire service defaults (telemetry, health check endpoints).

**src/rag-tools/literature_client.py** — Python client for the Research API LiteratureController, useful for scripting and evaluation.

### Document processing pipeline (async)

```
Blazor App
  → Service Bus (DownloadDocumentQueue)
    → DownloadRequestHandler (download PDF from PMC Open Data, store to Blob)
      → Service Bus (DocumentDownloadedTopic)
        → DocumentProcessingOrchestratorHandler (Chunk API → Embed API → Graph API)
          → Service Bus (DocumentGraphReadyTopic | DocumentProcessingFailedTopic)
        → BuildDocumentListHandler (save metadata to Table Storage)
```

### Key patterns and conventions

**Configuration validation** — Every config class has a `ThrowIfInvalid()` method called in `PostConfigure`. Startup fails immediately on missing required values rather than at first use.

**Key Vault toggle** — `SecureConfiguration.UseKeyVault: false` skips Key Vault loading for local development. Secrets can then be supplied via `appsettings.Development.json` or environment variables.

**Graph query resilience** — `GraphQueryClient` has a static circuit breaker (shared across scoped instances) with exponential backoff retries. Configured via `OpenAiConfiguration` (`GraphCircuitBreakerFailureThreshold`, `GraphCircuitBreakerBreakSeconds`, `GraphMaxRetryAttempts`, `GraphInitialBackoffMilliseconds`).

**RAG citation guardrails** — `AiClient` rejects LLM responses that lack `[Source n]` citations on factual bullet points, returning a fixed "I don't know" fallback instead. Controlled by `GraphRequireCitationsInResponse`. Evidence is filtered by `GraphEvidenceMinScore` (default 0.25) and capped at `GraphMaxEvidenceCount` (default 8).

**Azure Search index** (manual setup) — The `bio-docs` index requires fields: `PmcId` (string, filterable/sortable/searchable), `Doi`, `Title`, `PageText`, `PageNumber` (int, filterable/sortable), `FileName`, `Summary`, and `Vector` (Single collection, KNN, 1536 dimensions).



