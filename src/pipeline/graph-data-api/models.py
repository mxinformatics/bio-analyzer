from __future__ import annotations

from typing import Any, Optional

from pydantic import AliasChoices, BaseModel, ConfigDict, Field


class BuildGraphRequest(BaseModel):
    document_id: str = Field(..., description="Unique identifier for the document")


class BuildGraphResponse(BaseModel):
    document_id: str = Field(..., description="Document ID")
    doc_type: str = Field(..., description="Type of document")
    total_chunks: int = Field(..., description="Total number of chunks processed")
    nodes_created: int = Field(..., description="Total number of graph nodes created")
    message: str = Field(..., description="Status message")


class EnrichGraphRequest(BaseModel):
    document_id: str = Field(..., description="Unique identifier for the document")
    pmc_id: str = Field(default="", description="PubMed Central ID")
    authors: list[str] = Field(default_factory=list, description="List of author names")
    published_date: str = Field(default="", description="Publication date")
    publication_types: list[str] = Field(default_factory=list, description="Article publication types")
    topics: list[str] = Field(default_factory=list, description="Optional externally provided topics")


class EnrichGraphResponse(BaseModel):
    document_id: str = Field(..., description="Document ID")
    nodes_created: int = Field(..., description="Number of new nodes created")
    relationships_created: int = Field(..., description="Number of new relationships created")
    message: str = Field(..., description="Status message")


class QueryRequest(BaseModel):
    query: str = Field(..., description="Search query text")
    top_k: int = Field(default=5, ge=1, le=100)
    document_type: Optional[str] = Field(
        default=None,
        description="Optional single document type filter",
    )
    document_types: Optional[list[str]] = Field(
        default=None,
        description="Optional multi-document type filter",
    )


class ScoredDocumentResult(BaseModel):
    score: float = Field(..., description="Relevance score")
    text: str = Field(..., description="Chunk text")
    page_number: int = Field(..., description="Page number")
    chunk_index: int = Field(..., description="Chunk index")
    document_name: Optional[str] = Field(default=None)
    source_system_doc_id: Optional[str] = Field(
        default=None,
        validation_alias=AliasChoices("source_system_doc_id", "sharepoint_id"),
    )
    document_type: Optional[str] = Field(default=None)
    metadata: Optional[dict[str, Any]] = Field(default=None)

    model_config = ConfigDict(populate_by_name=True)

    @property
    def sharepoint_id(self) -> Optional[str]:
        return self.source_system_doc_id

    @sharepoint_id.setter
    def sharepoint_id(self, value: Optional[str]) -> None:
        self.source_system_doc_id = value


class QueryResponse(BaseModel):
    results: list[ScoredDocumentResult]
    query: str
    document_type: Optional[str] = None
    document_types: Optional[list[str]] = None
    total_results: int
