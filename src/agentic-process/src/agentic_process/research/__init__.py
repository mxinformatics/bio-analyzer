"""Client package for BioAnalyzer.Research.Api."""

from agentic_process.research.client import (
    ResearchApiClient,
    build_ingest_idempotency_key,
)
from agentic_process.research.exceptions import (
    ResearchApiConfigError,
    ResearchApiError,
    ResearchApiHttpError,
)
from agentic_process.research.models import (
    ArticleAbstract,
    ChatQueryResult,
    ChatQueryStatus,
    EntrezSearchResult,
    EntrezSummaryResult,
    LiteratureCandidate,
    LiteratureCandidatesResult,
    LiteratureIngestItem,
    LiteratureIngestJobStatusResult,
    LiteratureIngestResponse,
)

__all__ = [
    "ArticleAbstract",
    "ChatQueryResult",
    "ChatQueryStatus",
    "EntrezSearchResult",
    "EntrezSummaryResult",
    "LiteratureCandidate",
    "LiteratureCandidatesResult",
    "LiteratureIngestItem",
    "LiteratureIngestJobStatusResult",
    "LiteratureIngestResponse",
    "ResearchApiClient",
    "ResearchApiConfigError",
    "ResearchApiError",
    "ResearchApiHttpError",
    "build_ingest_idempotency_key",
]
