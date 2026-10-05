from __future__ import annotations

from pydantic import BaseModel, Field


class EmbedDocumentRequest(BaseModel):
    document_id: str = Field(..., description="Unique identifier for the document")


class EmbedDocumentResponse(BaseModel):
    document_id: str
    document_name: str
    total_chunks: int
    message: str


class EmbedTextRequest(BaseModel):
    text: str = Field(..., description="Text to be embedded")


class EmbedTextResponse(BaseModel):
    embedding: list[float] = Field(..., description="Embedding vector")
    dimension: int = Field(..., description="Embedding dimension")
