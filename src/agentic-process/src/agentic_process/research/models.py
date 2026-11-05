"""Pydantic models matching BioAnalyzer.Research.Api DTOs (camelCase JSON)."""

from __future__ import annotations

from datetime import datetime
from typing import Any

from pydantic import BaseModel, ConfigDict, Field


class _ApiModel(BaseModel):
    """Base model that accepts camelCase API payloads."""

    model_config = ConfigDict(
        populate_by_name=True,
        extra="ignore",
        str_strip_whitespace=True,
    )


# --- Literature search / summary / abstract ---------------------------------


class EntrezSearchResult(_ApiModel):
    count: str = ""
    ret_max: str = Field(default="", alias="retMax")
    ret_start: str = Field(default="", alias="retStart")
    id_list: list[str] = Field(default_factory=list, alias="idList")


class EntrezSummaryResult(_ApiModel):
    uid: str = ""
    title: str = ""
    pmc_id: str = Field(default="", alias="pmcId")
    doi: str = ""


class ArticleAbstract(_ApiModel):
    title: str = ""
    description: str = ""


class ArticleMetadataResult(_ApiModel):
    """Loose metadata envelope; API fields may grow over time."""

    model_config = ConfigDict(populate_by_name=True, extra="allow")

    pmc_id: str = Field(default="", alias="pmcId")
    title: str = ""
    doi: str = ""


class LiteratureDownloadLinkResult(_ApiModel):
    pmc_id: str = Field(default="", alias="pmcId")
    archive_link: str = Field(default="", alias="archiveLink")
    pdf_link: str = Field(default="", alias="pdfLink")


class LiteratureDownload(_ApiModel):
    download_link: str = Field(default="", alias="downloadLink")
    file_name: str = Field(default="", alias="fileName")
    title: str = ""
    pmc_id: str = Field(default="", alias="pmcId")
    doi: str = ""


class LiteratureDownloadList(_ApiModel):
    downloads: list[LiteratureDownload] = Field(default_factory=list)


class LiteratureAbstractBatchItem(_ApiModel):
    pmc_id: str = Field(default="", alias="pmcId")
    title: str = ""
    description: str = ""
    error_message: str | None = Field(default=None, alias="errorMessage")


class LiteratureAbstractBatchResult(_ApiModel):
    items: list[LiteratureAbstractBatchItem] = Field(default_factory=list)
    failures: list[LiteratureAbstractBatchItem] = Field(default_factory=list)


# --- Candidates / ingest ----------------------------------------------------


class LiteratureCandidate(_ApiModel):
    pmid: str = ""
    pmc_id: str = Field(default="", alias="pmcId")
    title: str = ""
    doi: str = ""
    pdf_link: str = Field(default="", alias="pdfLink")
    xml_link: str = Field(default="", alias="xmlLink")
    already_ingested: bool = Field(default=False, alias="alreadyIngested")
    rank: int = 0


class LiteratureCandidatesResult(_ApiModel):
    query: str = ""
    enabled: bool = True
    search_hit_count: int = Field(default=0, alias="searchHitCount")
    summaries_considered: int = Field(default=0, alias="summariesConsidered")
    link_resolutions_attempted: int = Field(default=0, alias="linkResolutionsAttempted")
    candidates: list[LiteratureCandidate] = Field(default_factory=list)
    skipped_reasons: list[str] = Field(default_factory=list, alias="skippedReasons")


class LiteratureIngestItem(_ApiModel):
    pmc_id: str = Field(alias="pmcId")
    title: str = ""
    doi: str = ""
    download_link: str | None = Field(default=None, alias="downloadLink")
    xml_link: str | None = Field(default=None, alias="xmlLink")


class LiteratureIngestEnqueueResult(_ApiModel):
    pmc_id: str = Field(default="", alias="pmcId")
    title: str = ""
    reason: str = ""
    download_link: str | None = Field(default=None, alias="downloadLink")


class LiteratureIngestResponse(_ApiModel):
    job_id: str = Field(default="", alias="jobId")
    status: str = "Accepted"
    correlation_id: str | None = Field(default=None, alias="correlationId")
    enqueued: list[LiteratureIngestEnqueueResult] = Field(default_factory=list)
    skipped: list[LiteratureIngestEnqueueResult] = Field(default_factory=list)
    replayed: bool = False


class LiteratureIngestItemStatus(_ApiModel):
    pmc_id: str = Field(default="", alias="pmcId")
    title: str = ""
    status: str = ""
    stage: str | None = None
    error_message: str | None = Field(default=None, alias="errorMessage")
    document_id: str | None = Field(default=None, alias="documentId")


class LiteratureIngestJobStatusResult(_ApiModel):
    job_id: str = Field(default="", alias="jobId")
    status: str = ""
    correlation_id: str | None = Field(default=None, alias="correlationId")
    requested_by: str | None = Field(default=None, alias="requestedBy")
    query_preview: str | None = Field(default=None, alias="queryPreview")
    created_utc: datetime | None = Field(default=None, alias="createdUtc")
    updated_utc: datetime | None = Field(default=None, alias="updatedUtc")
    error_message: str | None = Field(default=None, alias="errorMessage")
    items: list[LiteratureIngestItemStatus] = Field(default_factory=list)


# --- Chat / graph-RAG -------------------------------------------------------


class ChatQueryStatus:
    """Stable status values returned by POST /Chat/query."""

    ANSWERED = "Answered"
    NO_RESULTS = "NoResults"
    INSUFFICIENT_EVIDENCE = "InsufficientEvidence"
    GRAPH_UNAVAILABLE = "GraphUnavailable"


class ChatRetrievalSummary(_ApiModel):
    total_results: int = Field(default=0, alias="totalResults")
    selected_results: int = Field(default=0, alias="selectedResults")
    min_score: float = Field(default=0.0, alias="minScore")
    avg_score: float = Field(default=0.0, alias="avgScore")
    max_score: float = Field(default=0.0, alias="maxScore")
    threshold: float = 0.0
    top_k: int = Field(default=0, alias="topK")
    min_evidence_count: int = Field(default=0, alias="minEvidenceCount")
    max_evidence_count: int = Field(default=0, alias="maxEvidenceCount")
    retrieval_duration_ms: int = Field(default=0, alias="retrievalDurationMs")


class ChatEvidenceSource(_ApiModel):
    index: int = 0
    document_name: str = Field(default="", alias="documentName")
    source_system_doc_id: str = Field(default="", alias="sourceSystemDocId")
    pmc_id: str = Field(default="", alias="pmcId")
    page_number: int = Field(default=0, alias="pageNumber")
    chunk_index: int = Field(default=0, alias="chunkIndex")
    score: float = 0.0
    evidence: str = ""


class ChatQueryResult(_ApiModel):
    status: str = ChatQueryStatus.INSUFFICIENT_EVIDENCE
    query: str = ""
    query_hash: str = Field(default="", alias="queryHash")
    answer: str = ""
    retrieval: ChatRetrievalSummary = Field(default_factory=ChatRetrievalSummary)
    sources: list[ChatEvidenceSource] = Field(default_factory=list)
    retrieval_metric_status: str = Field(default="", alias="retrievalMetricStatus")
    error_message: str | None = Field(default=None, alias="errorMessage")

    @property
    def answered(self) -> bool:
        return self.status == ChatQueryStatus.ANSWERED


def literature_ingest_item_to_payload(item: LiteratureIngestItem | dict[str, Any]) -> dict[str, Any]:
    """Serialize an ingest item to camelCase JSON without client-controlled identity fields."""
    if isinstance(item, LiteratureIngestItem):
        data = item.model_dump(by_alias=True, exclude_none=True)
    else:
        data = {
            "pmcId": item.get("pmcId") or item.get("pmc_id") or "",
            "title": item.get("title") or "",
            "doi": item.get("doi") or "",
        }
        download_link = item.get("downloadLink") or item.get("download_link")
        xml_link = item.get("xmlLink") or item.get("xml_link")
        if download_link:
            data["downloadLink"] = download_link
        if xml_link:
            data["xmlLink"] = xml_link
    return data
