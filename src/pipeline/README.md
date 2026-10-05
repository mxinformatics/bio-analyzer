# BioAnalyzer pipeline APIs
This directory contains the Python services used by the document-processing pipeline:
- `chunk-api` (`POST /chunk`)
- `embed-api` (`POST /embed`, `POST /embedtext`)
- `graph-data-api` (`POST /build-graph`, `POST /query`)
- `pipeline-common` shared models/utilities
## Runtime requirements
The pipeline APIs use Azure Blob Storage for artifact IO and Neo4j for graph query/build operations.
There is no local filesystem storage fallback in the API runtime path.
## Setup
1. Install dependencies:
   - all services: `python3 -m pip install -r src/pipeline/requirements.txt`
   - chunk-api only: `python3 -m pip install -r src/pipeline/chunk-api/requirements.txt`
   - embed-api only: `python3 -m pip install -r src/pipeline/embed-api/requirements.txt`
   - graph-data-api only: `python3 -m pip install -r src/pipeline/graph-data-api/requirements.txt`
2. Create service-specific `.env` files from each `.env.example`.
3. Run services:
   - Uvicorn with service `.env` files:
     - `cd src/pipeline/chunk-api && uvicorn main:app --host 0.0.0.0 --port 8001 --env-file .env`
     - `cd src/pipeline/embed-api && uvicorn main:app --host 0.0.0.0 --port 8002 --env-file .env`
     - `cd src/pipeline/graph-data-api && uvicorn main:app --host 0.0.0.0 --port 8080 --env-file .env`
   - Docker Compose (loads `chunk-api/.env`, `embed-api/.env`, and `graph-data-api/.env` via `env_file`):
     - `docker compose -f src/pipeline/docker-compose.yml up --build`
4. If running these APIs through `src/BioAnalyzer/BioAnalyzer.AppHost/Program.cs`, keep `uvCertifiCaBundle` aligned with the venv Python version. If the venv moves from `python3.13` to a different version, update the `python3.13` segment in `.venv/lib/python3.13/site-packages/certifi/cacert.pem` accordingly.
## Shared Azure settings
- `AZURE_STORAGE_ACCOUNT`
- `AZURE_MANAGED_IDENTITY_CLIENT_ID` (optional; required when using user-assigned identity)
## chunk-api settings
- `AZURE_STORAGE_CHUNKS_CONTAINER`
- `AZURE_STORAGE_SOURCE_CONTAINER`
- `MAX_SOURCE_BYTES`
- `DEFAULT_CHUNK_SIZE`
- `DEFAULT_CHUNK_OVERLAP`
### Embedding (Azure OpenAI / Foundry, required by embed-api)
Set in `embed-api`:
- `FOUNDRY_AI_ENDPOINT`
- `FOUNDRY_AI_EMBEDDING_DEPLOYMENT`
Authentication:
- `embed-api` uses `DefaultAzureCredential` token auth only
- set `AZURE_MANAGED_IDENTITY_CLIENT_ID` when using user-assigned identity
Optional tuning:
- `EMBEDDING_BATCH_SIZE`
- `EMBEDDING_MAX_RETRIES` (capped at `1`; embed-api retries at most once)
Also required in `embed-api`:
- `AZURE_STORAGE_CHUNKS_CONTAINER`
- `AZURE_STORAGE_EMBEDDING_CONTAINER`
### Graph backend (Neo4j, required)
Set in `graph-data-api`:
- `NEO4J_URI`
- `NEO4J_DATABASE`
Credentials (Key Vault only):
- `KEY_VAULT_URL`
- `NEO4J_USERNAME_SECRET_NAME` (default `GraphDatabase--Username`)
- `NEO4J_PASSWORD_SECRET_NAME` (default `GraphDatabase--Password`)
Optional:
- `NEO4J_VECTOR_INDEX` (default `chunk_embedding_index`)
- `NEO4J_TEXT_INDEX` (default `chunk_text_index`)
- `NEO4J_VECTOR_DIMENSIONS` (default `512`)
Also required in `graph-data-api`:
- `AZURE_STORAGE_EMBEDDING_CONTAINER`
### Graph query embedding + hybrid ranking
Set in `graph-data-api`:
- `FOUNDRY_AI_ENDPOINT`
- `FOUNDRY_AI_EMBEDDING_DEPLOYMENT`
- `FOUNDRY_AI_CHAT_DEPLOYMENT` (used for content-derived topic extraction during enrich)
- `GRAPH_QUERY_ALPHA` (vector/full-text blend in range `0.0-1.0`)
- `TOPIC_EXTRACTION_MAX_CHUNKS` (default `12`)
- `TOPIC_EXTRACTION_MAX_CHUNK_CHARS` (default `1200`)
- `TOPIC_EXTRACTION_MAX_TOPICS` (default `12`)
Authentication:
- `graph-data-api` uses `DefaultAzureCredential` token auth only
- set `AZURE_MANAGED_IDENTITY_CLIENT_ID` when using user-assigned identity
## Validation
- Syntax check:
  - `python3 -m compileall src/pipeline`
- Unit tests:
  - `python3 -m unittest discover -s src/pipeline/tests -p 'test_*.py'`
