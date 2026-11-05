# agentic-process

Hands-on workspace for working through [*AI Agents in Action* (Manning)](https://www.manning.com/books/ai-agents-in-action-second-edition), backed by **BioAnalyzer.Research.Api** for literature search, OA candidate discovery, ingest, and graph-RAG.

## Setup

Requires [uv](https://docs.astral.sh/uv/) and Python 3.11+.

```bash
cd src/agentic-process
cp .env.example .env   # edit if your API URL/key differ
uv sync
```

Default local values match Research.Api Development settings:

- `RESEARCH_API_BASE_URL=http://localhost:5179`
- `RESEARCH_API_KEY=local-dev-research-api-key`

Start Research.Api (Aspire AppHost preferred, or `dotnet run` on the API project) before calling live endpoints.

## Research API client

Package layout:

```text
src/agentic_process/
  config.py                 # env / .env settings
  cli.py                    # smoke CLI
  research/
    client.py               # ResearchApiClient (httpx)
    models.py               # Pydantic DTOs
    exceptions.py
```

Example:

```python
from agentic_process.research import ResearchApiClient

with ResearchApiClient.from_env() as client:
    graph = client.ask_graph("What is known about BRCA1 mutations?")
    if not graph.answered:
        candidates = client.find_candidates("BRCA1 open access", max_candidates=3)
        print(candidates.candidates)
```

Auth: bearer token (`RESEARCH_API_BEARER_TOKEN`) wins over API key (`RESEARCH_API_KEY` / `X-Api-Key`). The client never sends `requestedBy` or `X-Requester-Id`.

## CLI smoke commands

```bash
uv run agentic-process search "CRISPR gene editing"
uv run agentic-process candidates "BRCA1 breast cancer" --max 3
uv run agentic-process graph "What evidence exists for PARP inhibitors in BRCA1?"
uv run agentic-process abstract PMC1234567
uv run agentic-process ingest PMC1234567 --title "Example" --query-preview "user question"
uv run agentic-process ingest-status <jobId>
```

## Tests

```bash
uv run pytest
uv run ruff check .
```

## Dependency management

Prefer `uv add <package>` so `pyproject.toml` and `uv.lock` stay in sync. Do not maintain a parallel hand-edited `requirements.txt`.


## Test Running API

````
curl -sS -H "X-Api-Key: local-dev-research-api-key" \
  -H "Accept: application/json" \
  "http://localhost:<port>/Literature?query=CRISPR&startIndex=0" | jq .
  ````

 

  curl -sS -H "X-Api-Key:  87BhyvNGhreqBjkmqiaedo" \
  -H "Accept: application/json" \
  "http://localhost:5179/Literature?query=CRISPR&startIndex=0" | jq .
