from __future__ import annotations

import hmac
import sys
import time
from pathlib import Path
from typing import Any, Optional

from fastapi import Depends, FastAPI, Header, HTTPException, Request

_pipeline_common_path = Path(__file__).resolve().parents[1] / "pipeline-common"
if _pipeline_common_path.exists():
    sys.path.insert(0, str(_pipeline_common_path))
from artifact_store import ChunkArtifactStore

from config import ChunkApiConfig
from models import ChunkDocumentRequest, ChunkDocumentResponse
from pipeline_common import (
    AzureBlobStorageClient,
    ChunkData,
    ChunkMetadata,
    configure_logging,
    get_correlation_id,
    install_request_observability,
    log_stage_metrics,
)
from source_resolver import SourceResolver
from text_processing import chunk_pages, parse_source_content

_logger = configure_logging(service_name="chunk-api", logger_name=__name__)
_config = ChunkApiConfig.from_env()
_blob_client = AzureBlobStorageClient(
    storage_account=_config.azure_storage_account,
    managed_identity_client_id=_config.azure_managed_identity_client_id or None,
)
_chunk_store = ChunkArtifactStore(
    blob_client=_blob_client,
    chunks_container=_config.azure_storage_chunks_container,
)
_source_resolver = SourceResolver(
    max_source_bytes=_config.max_source_bytes,
    storage_account=_config.azure_storage_account,
    source_container=_config.azure_source_container,
    managed_identity_client_id=_config.azure_managed_identity_client_id or None,
)

app = FastAPI(
    title="Chunk API",
    description="Chunks downloaded documents into structured text artifacts.",
    version="0.1.0",
)
install_request_observability(app, _logger, "chunk-api")


def _verify_api_key(x_api_key: Optional[str] = Header(default=None, alias="X-API-Key")) -> None:
    if not _config.require_api_key:
        return
    if not _config.chunk_api_key:
        raise HTTPException(status_code=500, detail="Chunk API key validation is enabled but not configured.")
    if not x_api_key:
        raise HTTPException(status_code=401, detail="X-API-Key header is required.")
    if not hmac.compare_digest(x_api_key, _config.chunk_api_key):
        raise HTTPException(status_code=403, detail="Invalid API key.")


def _normalize_request_metadata(raw_metadata: dict[str, Any] | None) -> dict[str, str]:
    normalized: dict[str, str] = {}
    if not raw_metadata:
        return normalized

    for raw_key, raw_value in raw_metadata.items():
        if not isinstance(raw_key, str):
            continue
        key = raw_key.strip()
        if not key:
            continue
        if raw_value is None:
            continue
        if isinstance(raw_value, str):
            value = raw_value.strip()
            if not value:
                continue
            normalized[key] = value
            continue
        normalized[key] = str(raw_value)
    return normalized


def _metadata_string(metadata: dict[str, str], keys: tuple[str, ...], default: str = "") -> str:
    for key in keys:
        value = metadata.get(key)
        if value:
            return value
    return default


def _collect_readiness_payload() -> dict[str, str | bool]:
    storage_ready, storage_detail = _chunk_store.readiness_check()
    source_ready, source_detail = _source_resolver.readiness_check()
    ready = storage_ready and source_ready
    return {
        "ready": ready,
        "storage_ready": storage_ready,
        "storage_detail": storage_detail,
        "source_ready": source_ready,
        "source_detail": source_detail,
        "storage_backend": _chunk_store.backend_name,
        "storage_location": _chunk_store.location_descriptor,
    }


@app.get("/")
async def root() -> dict[str, str]:
    return {"message": "Chunk API is running", "version": "0.1.0"}


@app.get("/health")
async def health() -> dict[str, str | bool]:
    readiness = _collect_readiness_payload()
    readiness["status"] = "healthy" if readiness["ready"] else "degraded"
    return readiness


@app.get("/ready")
async def ready() -> dict[str, str | bool]:
    readiness = _collect_readiness_payload()
    if not readiness["ready"]:
        raise HTTPException(status_code=503, detail=readiness)
    readiness["status"] = "ready"
    return readiness


@app.post("/chunk", response_model=ChunkDocumentResponse)
async def chunk_document(
    chunk_request: ChunkDocumentRequest,
    http_request: Request,
    _: None = Depends(_verify_api_key),
) -> ChunkDocumentResponse:
    correlation_id = get_correlation_id(http_request)
    chunk_started_at = time.perf_counter()

    try:
        _logger.info(
            "Chunk request received for document_id=%s correlation_id=%s",
            chunk_request.document_id,
            correlation_id,
        )
        request_metadata = _normalize_request_metadata(chunk_request.metadata)
        source_system_doc_id = _metadata_string(
            request_metadata,
            ("source_system_doc_id", "sharepoint_id", "pmc_id"),
            chunk_request.document_id,
        )
        doc_type = _metadata_string(
            request_metadata,
            ("doc_type", "document_type"),
            "biomedical_research_article",
        )
        original_file_name = _metadata_string(
            request_metadata,
            ("original_file_name",),
            chunk_request.file_name,
        )
        filtered_request_metadata = {
            key: value
            for key, value in request_metadata.items()
            if key not in {"location", "year", "publication_year"}
        }

        source_content, selected_source_type, selected_source_name, fallback_reason = await _source_resolver.resolve_content(
            chunk_request
        )
        parsed_pages = parse_source_content(source_content, selected_source_type)
        page_chunks = chunk_pages(parsed_pages, _config.chunk_size, _config.chunk_overlap)
        if not page_chunks:
            raise HTTPException(status_code=422, detail="Unable to extract chunkable text from source.")
        total_pages = max(page for page, _ in parsed_pages)

        chunk_models: list[ChunkData] = []
        for chunk_index, (page_number, text_value) in enumerate(page_chunks):
            chunk_metadata_payload: dict[str, Any] = {
                "document_id": chunk_request.document_id,
                "document_name": chunk_request.document_name,
                "source_system_doc_id": source_system_doc_id,
                "doc_type": doc_type,
                "original_file_name": original_file_name,
                "document_metadata": filtered_request_metadata,
                "chunk_index": chunk_index,
                "page": page_number,
                "total_pages": total_pages,
                "chunk_size": _config.chunk_size,
                "chunk_overlap": _config.chunk_overlap,
                "source_type": selected_source_type,
            }
            for key, value in filtered_request_metadata.items():
                if key not in chunk_metadata_payload and key != "document_metadata":
                    chunk_metadata_payload[key] = value
            chunk_models.append(
                ChunkData(
                    chunk_id=f"{chunk_request.document_id}:{chunk_index}",
                    chunk_index=chunk_index,
                    page_number=page_number,
                    content=text_value,
                    metadata=ChunkMetadata(**chunk_metadata_payload),
                )
            )

        written_chunk_files = _chunk_store.write_chunks(chunk_request.document_id, chunk_models)
        chunk_duration_ms = round((time.perf_counter() - chunk_started_at) * 1000.0, 2)

        log_stage_metrics(
            _logger,
            stage="chunk",
            correlation_id=correlation_id,
            outcome="success",
            document_id=chunk_request.document_id,
            total_pages=total_pages,
            total_chunks=len(chunk_models),
            source_type=selected_source_type,
            source_size_bytes=len(source_content),
            written_files=len(written_chunk_files),
            duration_ms=chunk_duration_ms,
        )

        return ChunkDocumentResponse(
            document_name=chunk_request.document_name,
            document_id=chunk_request.document_id,
            size_bytes=len(source_content),
            total_pages=total_pages,
            total_chunks=len(chunk_models),
            message="Successfully chunked document",
            selected_source_type=selected_source_type,
            selected_source_name=selected_source_name,
            source_fallback_reason=fallback_reason,
        )
    except HTTPException:
        log_stage_metrics(
            _logger,
            stage="chunk",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=chunk_request.document_id,
            duration_ms=round((time.perf_counter() - chunk_started_at) * 1000.0, 2),
        )
        raise
    except ValueError as ex:
        log_stage_metrics(
            _logger,
            stage="chunk",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=chunk_request.document_id,
            error_type="ValueError",
            duration_ms=round((time.perf_counter() - chunk_started_at) * 1000.0, 2),
        )
        _logger.error("Chunking validation failed for document_id=%s: %s", chunk_request.document_id, ex)
        raise HTTPException(status_code=400, detail=str(ex)) from ex
    except Exception as ex:  # noqa: BLE001
        log_stage_metrics(
            _logger,
            stage="chunk",
            correlation_id=correlation_id,
            outcome="failed",
            document_id=chunk_request.document_id,
            error_type=type(ex).__name__,
            duration_ms=round((time.perf_counter() - chunk_started_at) * 1000.0, 2),
        )
        _logger.exception("Chunking failed for document_id=%s", chunk_request.document_id)
        raise HTTPException(status_code=500, detail=f"Chunk processing failed: {ex}") from ex
