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
from artifact_store import EmbedArtifactStore

from config import EmbedApiConfig
from embedder import AzureOpenAiTextEmbedder
from models import EmbedDocumentRequest, EmbedDocumentResponse, EmbedTextRequest, EmbedTextResponse
from pipeline_common import (
    AzureBlobStorageClient,
    EmbeddedChunkData,
    configure_logging,
    get_correlation_id,
    install_request_observability,
    log_stage_metrics,
)

_logger = configure_logging(service_name="embed-api", logger_name=__name__)
_config = EmbedApiConfig.from_env()
_blob_client = AzureBlobStorageClient(
    storage_account=_config.azure_storage_account,
    managed_identity_client_id=_config.azure_managed_identity_client_id or None,
)
_artifact_store = EmbedArtifactStore(
    blob_client=_blob_client,
    chunks_container=_config.azure_storage_chunks_container,
    embedded_chunks_container=_config.azure_storage_embedding_container,
)
_embedder = AzureOpenAiTextEmbedder(
    endpoint=_config.foundry_ai_endpoint,
    embedding_deployment=_config.foundry_ai_embedding_deployment,
    managed_identity_client_id=_config.azure_managed_identity_client_id or None,
    batch_size=_config.embedding_batch_size,
    max_retries=_config.embedding_max_retries,
)
_resolved_embedding_provider = "azure_openai"

app = FastAPI(
    title="Embed API",
    description="Generates embeddings for chunk artifacts.",
    version="0.1.0",
)
install_request_observability(app, _logger, "embed-api")


def _verify_api_key(x_api_key: Optional[str] = Header(default=None, alias="X-API-Key")) -> None:
    if not _config.require_api_key:
        return
    if not _config.embedding_api_key:
        raise HTTPException(status_code=500, detail="Embedding API key validation is enabled but not configured.")
    if not x_api_key:
        raise HTTPException(status_code=401, detail="X-API-Key header is required.")
    if not hmac.compare_digest(x_api_key, _config.embedding_api_key):
        raise HTTPException(status_code=403, detail="Invalid API key.")


def _collect_readiness_payload() -> dict[str, str | int | bool]:
    storage_ready, storage_detail = _artifact_store.readiness_check()
    embedder_ready, embedder_detail = _embedder.readiness_check()
    ready = storage_ready and embedder_ready
    return {
        "ready": ready,
        "storage_ready": storage_ready,
        "storage_detail": storage_detail,
        "embedding_provider_ready": embedder_ready,
        "embedding_provider_detail": embedder_detail,
        "embedding_provider": _resolved_embedding_provider,
        "storage_backend": _artifact_store.backend_name,
        "storage_location": _artifact_store.location_descriptor,
        "embedding_dimension": _embedder.dimension,
    }


@app.get("/")
async def root() -> dict[str, str]:
    return {"message": "Embedding API is running", "version": "0.1.0"}


@app.get("/health")
async def health() -> dict[str, str | int | bool]:
    readiness = _collect_readiness_payload()
    readiness["status"] = "healthy" if readiness["ready"] else "degraded"
    return readiness


@app.get("/ready")
async def ready() -> dict[str, str | int | bool]:
    readiness = _collect_readiness_payload()
    if not readiness["ready"]:
        raise HTTPException(status_code=503, detail=readiness)
    readiness["status"] = "ready"
    return readiness


@app.post("/embedtext", response_model=EmbedTextResponse)
async def embed_text(
    embed_text_request: EmbedTextRequest,
    http_request: Request,
    _: None = Depends(_verify_api_key),
) -> EmbedTextResponse:
    correlation_id = get_correlation_id(http_request)
    embed_text_started_at = time.perf_counter()
    try:
        if not embed_text_request.text.strip():
            raise HTTPException(status_code=422, detail="Text cannot be empty.")
        embedding = _embedder.embed_text(embed_text_request.text)
        duration_ms = round((time.perf_counter() - embed_text_started_at) * 1000.0, 2)
        log_stage_metrics(
            _logger,
            stage="embedtext",
            correlation_id=correlation_id,
            outcome="success",
            text_length=len(embed_text_request.text),
            embedding_dimension=len(embedding),
            duration_ms=duration_ms,
        )
        return EmbedTextResponse(embedding=embedding, dimension=len(embedding))
    except HTTPException:
        log_stage_metrics(
            _logger,
            stage="embedtext",
            correlation_id=correlation_id,
            outcome="failed",
            text_length=len(embed_text_request.text or ""),
            duration_ms=round((time.perf_counter() - embed_text_started_at) * 1000.0, 2),
        )
        raise
    except Exception as ex:  # noqa: BLE001
        log_stage_metrics(
            _logger,
            stage="embedtext",
            correlation_id=correlation_id,
            outcome="failed",
            text_length=len(embed_text_request.text or ""),
            error_type=type(ex).__name__,
            duration_ms=round((time.perf_counter() - embed_text_started_at) * 1000.0, 2),
        )
        _logger.exception(
            "Embed text failed. correlation_id=%s endpoint=%s deployment=%s",
            correlation_id,
            _config.foundry_ai_endpoint,
            _config.foundry_ai_embedding_deployment,
        )
        raise HTTPException(status_code=500, detail=f"Embed text failed: {ex}") from ex


@app.post("/embed", response_model=EmbedDocumentResponse)
async def embed_document(
    embed_request: EmbedDocumentRequest,
    http_request: Request,
    _: None = Depends(_verify_api_key),
) -> EmbedDocumentResponse:
    correlation_id = get_correlation_id(http_request)
    embed_started_at = time.perf_counter()
    chunk_count = 0
    total_chunk_chars = 0
    estimated_batches = 0
    min_chunk_chars = 0
    max_chunk_chars = 0
    try:
        _logger.info(
            "Embedding request received for document_id=%s correlation_id=%s",
            embed_request.document_id,
            correlation_id,
        )
        chunks = _artifact_store.read_chunks(embed_request.document_id)
        if not chunks:
            raise HTTPException(
                status_code=404,
                detail=f"No chunk artifacts found for document_id '{embed_request.document_id}'.",
            )
        chunk_count = len(chunks)
        if chunk_count > _config.max_chunks_per_request:
            raise HTTPException(
                status_code=422,
                detail=(
                    f"Chunk count {chunk_count} exceeds MAX_CHUNKS_PER_REQUEST "
                    f"({_config.max_chunks_per_request})."
                ),
            )
        first_chunk = chunks[0]
        document_name = first_chunk.metadata.document_name or embed_request.document_id
        chunk_texts = [chunk.content for chunk in chunks]
        chunk_text_lengths = [len(text_value) for text_value in chunk_texts]
        total_chunk_chars = sum(chunk_text_lengths)
        min_chunk_chars = min(chunk_text_lengths)
        max_chunk_chars = max(chunk_text_lengths)
        estimated_batches = (
            chunk_count + _config.embedding_batch_size - 1
        ) // _config.embedding_batch_size

        embeddings = _embedder.embed_texts(chunk_texts)
        if len(embeddings) != chunk_count:
            raise RuntimeError(
                "Embedding provider returned an unexpected number of vectors. "
                f"expected={chunk_count} actual={len(embeddings)}"
            )
        embedded_chunks: list[EmbeddedChunkData] = []
        for chunk, embedding in zip(chunks, embeddings):
            embedded_chunks.append(
                EmbeddedChunkData(
                    chunk_id=chunk.chunk_id,
                    chunk_index=chunk.chunk_index,
                    page_number=chunk.page_number,
                    content=chunk.content,
                    metadata=chunk.metadata,
                    embedding=embedding,
                )
            )

        _artifact_store.write_embedded_chunks(embed_request.document_id, embedded_chunks)
        duration_ms = round((time.perf_counter() - embed_started_at) * 1000.0, 2)
        log_stage_metrics(
            _logger,
            stage="embed",
            correlation_id=correlation_id,
            outcome="success",
            document_id=embed_request.document_id,
            total_chunks=len(embedded_chunks),
            embedding_dimension=_embedder.dimension,
            total_chunk_chars=total_chunk_chars,
            estimated_batches=estimated_batches,
            duration_ms=duration_ms,
        )

        return EmbedDocumentResponse(
            document_id=embed_request.document_id,
            document_name=document_name,
            total_chunks=len(embedded_chunks),
            message="Successfully embedded document chunks",
        )
    except HTTPException:
        log_stage_metrics(
            _logger,
            stage="embed",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=embed_request.document_id,
            total_chunks=chunk_count,
            total_chunk_chars=total_chunk_chars,
            estimated_batches=estimated_batches,
            duration_ms=round((time.perf_counter() - embed_started_at) * 1000.0, 2),
        )
        raise
    except Exception as ex:  # noqa: BLE001
        avg_chunk_chars = round(total_chunk_chars / chunk_count, 2) if chunk_count else 0.0
        log_stage_metrics(
            _logger,
            stage="embed",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=embed_request.document_id,
            total_chunks=chunk_count,
            total_chunk_chars=total_chunk_chars,
            estimated_batches=estimated_batches,
            error_type=type(ex).__name__,
            duration_ms=round((time.perf_counter() - embed_started_at) * 1000.0, 2),
        )
        _logger.exception(
            "Embedding failed for document_id=%s correlation_id=%s total_chunks=%s "
            "total_chunk_chars=%s min_chunk_chars=%s max_chunk_chars=%s avg_chunk_chars=%s "
            "estimated_batches=%s configured_batch_size=%s endpoint=%s deployment=%s "
            "error_type=%s error_message=%s",
            embed_request.document_id,
            correlation_id,
            chunk_count,
            total_chunk_chars,
            min_chunk_chars,
            max_chunk_chars,
            avg_chunk_chars,
            estimated_batches,
            _config.embedding_batch_size,
            _config.foundry_ai_endpoint,
            _config.foundry_ai_embedding_deployment,
            type(ex).__name__,
            str(ex)[:2000],
        )
        raise HTTPException(status_code=500, detail=f"Embedding failed: {ex}") from ex
