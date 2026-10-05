from __future__ import annotations

from typing import Any, Optional

from pydantic import AliasChoices, BaseModel, ConfigDict, Field


class ChunkMetadata(BaseModel):
    producer: Optional[str] = None
    creator: Optional[str] = None
    creationdate: Optional[str] = None
    title: Optional[str] = None
    author: Optional[str] = None
    moddate: Optional[str] = None
    source: Optional[str] = None
    total_pages: Optional[int] = None
    page: Optional[int] = None
    page_label: Optional[str] = None
    chunk_method: Optional[str] = None
    char_count: Optional[int] = None
    doc_type: Optional[str] = None
    document_id: Optional[str] = None
    document_name: Optional[str] = None
    source_system_doc_id: Optional[str] = Field(
        default=None,
        validation_alias=AliasChoices("source_system_doc_id", "sharepoint_id"),
    )
    link: Optional[str] = None
    original_file_name: Optional[str] = None
    chunk_size: Optional[int] = None
    chunk_overlap: Optional[int] = None
    chunk_index: Optional[int] = None
    source_type: Optional[str] = None
    section_type: Optional[str] = None
    section_title: Optional[str] = None
    citation_count: Optional[int] = None
    has_citations: Optional[bool] = None
    document_metadata: Optional[dict[str, Any]] = None

    model_config = ConfigDict(extra="allow", populate_by_name=True)

    @property
    def sharepoint_id(self) -> Optional[str]:
        return self.source_system_doc_id

    @sharepoint_id.setter
    def sharepoint_id(self, value: Optional[str]) -> None:
        self.source_system_doc_id = value


class ChunkData(BaseModel):
    chunk_id: str = Field(..., description="Unique identifier for the chunk")
    chunk_index: int = Field(..., description="Index of the chunk within the document")
    page_number: int = Field(..., description="Page number where this chunk appears")
    content: str = Field(..., description="Text content of the chunk")
    metadata: ChunkMetadata = Field(..., description="Metadata associated with the chunk")


class EmbeddedChunkData(BaseModel):
    chunk_id: str = Field(..., description="Unique identifier for the chunk")
    chunk_index: int = Field(..., description="Index of the chunk within the document")
    page_number: int = Field(..., description="Page number where this chunk appears")
    content: str = Field(..., description="Text content of the chunk")
    metadata: ChunkMetadata = Field(..., description="Metadata associated with the chunk")
    embedding: list[float] = Field(..., description="Vector embedding of the chunk content")


class GraphDocument(BaseModel):
    document_id: str
    doc_type: str
    total_chunks: int
    nodes_created: int
    chunks: list[EmbeddedChunkData]
    metadata: dict[str, Any] = Field(default_factory=dict)
