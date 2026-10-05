from __future__ import annotations

import hmac
import sys
import time
from pathlib import Path
from typing import Optional

from fastapi import Depends, FastAPI, Header, HTTPException, Request

_pipeline_common_path = Path(__file__).resolve().parents[1] / "pipeline-common"
if _pipeline_common_path.exists():
    sys.path.insert(0, str(_pipeline_common_path))
from artifact_store import GraphArtifactStore

from config import GraphDataApiConfig
from embedder import AzureOpenAiTextEmbedder
from graph_repository import Neo4jGraphRepository
from models import BuildGraphRequest, BuildGraphResponse, EnrichGraphRequest, EnrichGraphResponse, QueryRequest, QueryResponse
from topic_extractor import AzureOpenAiTopicExtractor
from pipeline_common import (
    AzureBlobStorageClient,
    configure_logging,
    get_correlation_id,
    install_request_observability,
    log_stage_metrics,
)

_logger = configure_logging(service_name="graph-data-api", logger_name=__name__)
_config = GraphDataApiConfig.from_env()
_blob_client = AzureBlobStorageClient(
    storage_account=_config.azure_storage_account,
    managed_identity_client_id=_config.azure_managed_identity_client_id or None,
)
_artifact_store = GraphArtifactStore(
    blob_client=_blob_client,
    embedded_chunks_container=_config.azure_storage_embedding_container,
)
_query_embedder = AzureOpenAiTextEmbedder(
    endpoint=_config.foundry_ai_endpoint,
    embedding_deployment=_config.foundry_ai_embedding_deployment,
    managed_identity_client_id=_config.azure_managed_identity_client_id or None,
)
_topic_extractor = AzureOpenAiTopicExtractor(
    endpoint=_config.foundry_ai_endpoint,
    chat_deployment=_config.foundry_ai_chat_deployment,
    managed_identity_client_id=_config.azure_managed_identity_client_id or None,
    max_chunks=_config.topic_extraction_max_chunks,
    max_chunk_chars=_config.topic_extraction_max_chunk_chars,
    max_topics=_config.topic_extraction_max_topics,
)
_resolved_query_embedding_provider = "azure_openai"
_graph_backend_name = "neo4j"
_graph_repository = Neo4jGraphRepository(
    uri=_config.neo4j_uri,
    username=_config.neo4j_username,
    password=_config.neo4j_password,
    database=_config.neo4j_database,
    max_query_results=_config.max_query_results,
    query_embedder=_query_embedder,
    vector_index=_config.neo4j_vector_index,
    text_index=_config.neo4j_text_index,
    vector_dimensions=_config.neo4j_vector_dimensions,
    hybrid_alpha=_config.graph_query_alpha,
)

app = FastAPI(
    title="Graph Data API",
    description="Builds and queries graph-style projections for embedded chunks.",
    version="0.1.0",
)
install_request_observability(app, _logger, "graph-data-api")


def _verify_api_key(x_api_key: Optional[str] = Header(default=None, alias="X-API-Key")) -> None:
    if not _config.require_api_key:
        return
    if not _config.graph_data_api_key:
        raise HTTPException(status_code=500, detail="Graph API key validation is enabled but not configured.")
    if not x_api_key:
        raise HTTPException(status_code=401, detail="X-API-Key header is required.")
    if not hmac.compare_digest(x_api_key, _config.graph_data_api_key):
        raise HTTPException(status_code=403, detail="Invalid API key.")


def _collect_readiness_payload() -> dict[str, str | int | float | bool]:
    storage_ready, storage_detail = _artifact_store.readiness_check()
    graph_ready, graph_detail = _graph_repository.readiness_check()
    query_provider_ready, query_provider_detail = _query_embedder.readiness_check()
    query_provider_required = True
    ready = storage_ready and graph_ready and ((not query_provider_required) or query_provider_ready)
    return {
        "ready": ready,
        "storage_ready": storage_ready,
        "storage_detail": storage_detail,
        "graph_backend_ready": graph_ready,
        "graph_backend_detail": graph_detail,
        "graph_backend": _graph_backend_name,
        "query_embedding_provider_required": query_provider_required,
        "query_embedding_provider_ready": query_provider_ready,
        "query_embedding_provider_detail": query_provider_detail,
        "query_embedding_provider": _resolved_query_embedding_provider,
        "query_embedding_dimension": _query_embedder.dimension,
        "query_hybrid_alpha": _config.graph_query_alpha,
        "topic_extraction_enabled": _topic_extractor.is_enabled,
        "topic_extraction_disabled_reason": _topic_extractor.disabled_reason,
        "storage_backend": _artifact_store.backend_name,
        "storage_location": _artifact_store.location_descriptor,
    }


@app.get("/")
async def root() -> dict[str, str]:
    return {"message": "Graph Data API is running", "version": "0.1.0"}


@app.get("/health")
async def health() -> dict[str, str | int | float | bool]:
    readiness = _collect_readiness_payload()
    readiness["status"] = "healthy" if readiness["ready"] else "degraded"
    return readiness


@app.get("/ready")
async def ready() -> dict[str, str | int | float | bool]:
    readiness = _collect_readiness_payload()
    if not readiness["ready"]:
        raise HTTPException(status_code=503, detail=readiness)
    readiness["status"] = "ready"
    return readiness


@app.post("/build-graph", response_model=BuildGraphResponse)
async def build_graph(
    request: BuildGraphRequest,
    http_request: Request,
    _: None = Depends(_verify_api_key),
) -> BuildGraphResponse:
    correlation_id = get_correlation_id(http_request)
    build_started_at = time.perf_counter()

    try:
        _logger.info(
            "Build-graph request received for document_id=%s correlation_id=%s",
            request.document_id,
            correlation_id,
        )
        embedded_chunks = _artifact_store.read_embedded_chunks(request.document_id)
        if not embedded_chunks:
            raise HTTPException(
                status_code=404,
                detail=f"No embedded chunks found for document_id '{request.document_id}'.",
            )

        graph_document = _graph_repository.build_graph(request.document_id, embedded_chunks)

        duration_ms = round((time.perf_counter() - build_started_at) * 1000.0, 2)
        log_stage_metrics(
            _logger,
            stage="graph_build",
            correlation_id=correlation_id,
            outcome="success",
            document_id=request.document_id,
            total_chunks=graph_document.total_chunks,
            nodes_created=graph_document.nodes_created,
            graph_backend=_graph_backend_name,
            duration_ms=duration_ms,
        )

        return BuildGraphResponse(
            document_id=request.document_id,
            doc_type=graph_document.doc_type,
            total_chunks=graph_document.total_chunks,
            nodes_created=graph_document.nodes_created,
            message="Successfully built graph document projection",
        )
    except HTTPException:
        log_stage_metrics(
            _logger,
            stage="graph_build",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=request.document_id,
            graph_backend=_graph_backend_name,
            duration_ms=round((time.perf_counter() - build_started_at) * 1000.0, 2),
        )
        raise
    except Exception as ex:  # noqa: BLE001
        log_stage_metrics(
            _logger,
            stage="graph_build",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=request.document_id,
            graph_backend=_graph_backend_name,
            error_type=type(ex).__name__,
            duration_ms=round((time.perf_counter() - build_started_at) * 1000.0, 2),
        )
        _logger.exception("Graph build failed for document_id=%s", request.document_id)
        raise HTTPException(status_code=500, detail=f"Graph build failed: {ex}") from ex


@app.post("/enrich-graph", response_model=EnrichGraphResponse)
async def enrich_graph(
    request: EnrichGraphRequest,
    http_request: Request,
    _: None = Depends(_verify_api_key),
) -> EnrichGraphResponse:
    correlation_id = get_correlation_id(http_request)
    enrich_started_at = time.perf_counter()

    try:
        _logger.info(
            "Enrich-graph request received for document_id=%s correlation_id=%s",
            request.document_id,
            correlation_id,
        )
        derived_topics: list[str] = []
        topic_source = "None"
        if _topic_extractor.is_enabled:
            try:
                embedded_chunks = _artifact_store.read_embedded_chunks(request.document_id)
                derived_topics = _topic_extractor.extract_topics(
                    embedded_chunks=embedded_chunks,
                    publication_types=request.publication_types,
                )
            except Exception as ex:  # noqa: BLE001
                _logger.warning(
                    "Topic extraction failed for document_id=%s correlation_id=%s error=%s",
                    request.document_id,
                    correlation_id,
                    ex,
                )

        topics = _merge_topics(request.topics, derived_topics)
        if derived_topics:
            topic_source = "LLM"
        elif request.topics:
            topic_source = "Provided"
        nodes_created, relationships_created = _graph_repository.enrich_graph(
            document_id=request.document_id,
            pmc_id=request.pmc_id,
            authors=request.authors,
            published_date=request.published_date,
            publication_types=request.publication_types,
            topics=topics,
            topic_source=topic_source,
        )

        duration_ms = round((time.perf_counter() - enrich_started_at) * 1000.0, 2)
        log_stage_metrics(
            _logger,
            stage="graph_enrich",
            correlation_id=correlation_id,
            outcome="success",
            document_id=request.document_id,
            nodes_created=nodes_created,
            relationships_created=relationships_created,
            graph_backend=_graph_backend_name,
            duration_ms=duration_ms,
        )

        return EnrichGraphResponse(
            document_id=request.document_id,
            nodes_created=nodes_created,
            relationships_created=relationships_created,
            message="Successfully enriched graph document",
        )
    except HTTPException:
        log_stage_metrics(
            _logger,
            stage="graph_enrich",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=request.document_id,
            graph_backend=_graph_backend_name,
            duration_ms=round((time.perf_counter() - enrich_started_at) * 1000.0, 2),
        )
        raise
    except Exception as ex:  # noqa: BLE001
        log_stage_metrics(
            _logger,
            stage="graph_enrich",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=request.document_id,
            graph_backend=_graph_backend_name,
            error_type=type(ex).__name__,
            duration_ms=round((time.perf_counter() - enrich_started_at) * 1000.0, 2),
        )
        _logger.exception("Graph enrichment failed for document_id=%s", request.document_id)
        raise HTTPException(status_code=500, detail=f"Graph enrichment failed: {ex}") from ex


@app.post("/query", response_model=QueryResponse)
async def query_graph(
    request: QueryRequest,
    http_request: Request,
    _: None = Depends(_verify_api_key),
) -> QueryResponse:
    correlation_id = get_correlation_id(http_request)
    query_started_at = time.perf_counter()

    try:
        ranked = _graph_repository.query(request)
        duration_ms = round((time.perf_counter() - query_started_at) * 1000.0, 2)
        log_stage_metrics(
            _logger,
            stage="graph_query",
            correlation_id=correlation_id,
            outcome="success",
            graph_backend=_graph_backend_name,
            query_length=len(request.query),
            top_k=request.top_k,
            total_results=len(ranked),
            duration_ms=duration_ms,
        )
        return QueryResponse(
            results=ranked,
            query=request.query,
            document_type=request.document_type,
            document_types=request.document_types,
            total_results=len(ranked),
        )
    except HTTPException:
        log_stage_metrics(
            _logger,
            stage="graph_query",
            correlation_id=correlation_id,
            outcome="failed",
            graph_backend=_graph_backend_name,
            top_k=request.top_k,
            duration_ms=round((time.perf_counter() - query_started_at) * 1000.0, 2),
        )
        raise
    except Exception as ex:  # noqa: BLE001
        log_stage_metrics(
            _logger,
            stage="graph_query",
            correlation_id=correlation_id,
            outcome="failed",
            graph_backend=_graph_backend_name,
            top_k=request.top_k,
            error_type=type(ex).__name__,
            duration_ms=round((time.perf_counter() - query_started_at) * 1000.0, 2),
        )
        _logger.exception("Graph query failed.")
        raise HTTPException(status_code=500, detail=f"Graph query failed: {ex}") from ex


@app.on_event("shutdown")
async def shutdown_event() -> None:
    try:
        _graph_repository.close()
    except Exception as ex:  # noqa: BLE001
        _logger.warning("Graph repository shutdown cleanup failed. error=%s", ex)


def _merge_topics(provided_topics: list[str], derived_topics: list[str]) -> list[str]:
    merged: list[str] = []
    seen: set[str] = set()
    for topic in [*provided_topics, *derived_topics]:
        if not isinstance(topic, str):
            continue
        normalized = topic.strip()
        if not normalized:
            continue
        key = normalized.casefold()
        if key in seen:
            continue
        seen.add(key)
        merged.append(normalized)
    return merged
