from __future__ import annotations

from typing import Any

from pydantic import BaseModel, Field


class ChunkDocumentRequest(BaseModel):
    document_name: str = Field(..., description="Human-readable document name")
    file_name: str = Field(..., description="Source file name")
    document_id: str = Field(..., description="Stable pipeline document identifier")
    metadata: dict[str, Any] = Field(default_factory=dict, description="Additional metadata payload")


class ChunkDocumentResponse(BaseModel):
    document_name: str
    document_id: str
    size_bytes: int
    total_pages: int
    total_chunks: int
    message: str
    selected_source_type: str = ""
    selected_source_name: str = ""
    source_fallback_reason: str = ""
