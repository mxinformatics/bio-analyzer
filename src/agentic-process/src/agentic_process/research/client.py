"""Authenticated HTTP client for BioAnalyzer.Research.Api."""

from __future__ import annotations

import hashlib
import logging
from collections.abc import Iterable, Mapping, Sequence
from typing import Any, Self
from urllib.parse import urlencode

import httpx

from agentic_process.config import ResearchApiSettings, load_research_api_settings
from agentic_process.research.exceptions import ResearchApiError, ResearchApiHttpError
from agentic_process.research.models import (
    ArticleAbstract,
    ArticleMetadataResult,
    ChatQueryResult,
    EntrezSearchResult,
    EntrezSummaryResult,
    LiteratureAbstractBatchResult,
    LiteratureCandidatesResult,
    LiteratureDownloadLinkResult,
    LiteratureDownloadList,
    LiteratureIngestItem,
    LiteratureIngestJobStatusResult,
    LiteratureIngestResponse,
    literature_ingest_item_to_payload,
)

_logger = logging.getLogger(__name__)


def build_ingest_idempotency_key(
    query_preview: str | None,
    items: Sequence[Mapping[str, Any] | LiteratureIngestItem],
) -> str:
    """
    Stable per-turn idempotency key from query preview + sorted PMCIDs.

    Mirrors bioanalyzer-chat ResearchApi.buildIngestIdempotencyKey (SHA-256 hex).
    """
    pmc_ids: list[str] = []
    for item in items:
        if isinstance(item, LiteratureIngestItem):
            pmc_id = item.pmc_id
        else:
            pmc_id = str(item.get("pmcId") or item.get("pmc_id") or "")
        pmc_id = pmc_id.strip().upper()
        if pmc_id:
            pmc_ids.append(pmc_id)
    pmc_ids = sorted(set(pmc_ids))
    material = f"{(query_preview or '').strip()}|{','.join(pmc_ids)}"
    return hashlib.sha256(material.encode("utf-8")).hexdigest()


class ResearchApiClient:
    """
    Python client for BioAnalyzer.Research.Api literature + graph-RAG endpoints.

    Auth: prefer bearer token; otherwise send shared API key via X-Api-Key.
    Never send client-controlled requestedBy / X-Requester-Id — the API binds
    requester identity from the authenticated principal only.
    """

    def __init__(
        self,
        settings: ResearchApiSettings | None = None,
        *,
        client: httpx.Client | None = None,
        timeout: float | None = None,
    ) -> None:
        self._settings = settings or load_research_api_settings()
        self._owns_client = client is None
        self._client = client or httpx.Client(
            base_url=self._settings.base_url.rstrip("/"),
            timeout=timeout if timeout is not None else self._settings.timeout_seconds,
        )

    @classmethod
    def from_env(cls, **kwargs: Any) -> ResearchApiClient:
        """Construct a client from environment / .env settings."""
        return cls(load_research_api_settings(), **kwargs)

    @property
    def base_url(self) -> str:
        return self._settings.base_url.rstrip("/")

    def close(self) -> None:
        if self._owns_client:
            self._client.close()

    def __enter__(self) -> Self:
        return self

    def __exit__(self, *args: object) -> None:
        self.close()

    # --- internal HTTP helpers ----------------------------------------------

    def _auth_headers(self, extra: Mapping[str, str] | None = None) -> dict[str, str]:
        headers: dict[str, str] = {"Accept": "application/json"}
        if extra:
            headers.update(dict(extra))

        if self._settings.bearer_token:
            headers["Authorization"] = f"Bearer {self._settings.bearer_token}"
        elif self._settings.api_key:
            headers[self._settings.api_key_header] = self._settings.api_key
        return headers

    def _raise_for_status(self, response: httpx.Response, action: str) -> None:
        if response.is_success:
            return
        body = response.text
        message = f"Research API {action} failed: {response.status_code} {response.reason_phrase}."
        if body:
            message = f"{message} {body}"
        raise ResearchApiHttpError(message, status_code=response.status_code, body=body)

    def _get_json(self, path: str, *, params: Mapping[str, Any] | None = None, action: str) -> Any:
        _logger.debug("GET %s params=%s", path, params)
        response = self._client.get(path, params=params, headers=self._auth_headers())
        self._raise_for_status(response, action)
        return response.json()

    def _post_json(
        self,
        path: str,
        *,
        payload: Mapping[str, Any],
        action: str,
        extra_headers: Mapping[str, str] | None = None,
    ) -> Any:
        headers = self._auth_headers({"Content-Type": "application/json"})
        if extra_headers:
            headers.update(dict(extra_headers))
        _logger.debug("POST %s", path)
        response = self._client.post(path, json=dict(payload), headers=headers)
        self._raise_for_status(response, action)
        return response.json()

    # --- Literature: discovery ----------------------------------------------

    def search_literature(self, query: str, start_index: int = 0) -> EntrezSearchResult:
        if not query or not query.strip():
            raise ValueError("Query cannot be null or empty.")
        data = self._get_json(
            "/Literature",
            params={"query": query.strip(), "startIndex": start_index},
            action="searchLiterature",
        )
        return EntrezSearchResult.model_validate(data)

    def get_summaries(self, ids: Sequence[str]) -> list[EntrezSummaryResult]:
        if not ids:
            return []
        # ASP.NET binds repeated ids= query keys for IList<string>
        query = urlencode([("ids", i) for i in ids])
        data = self._get_json(f"/Literature/summary?{query}", action="getLiteratureSummary")
        if not isinstance(data, list):
            raise ResearchApiError("Expected a JSON array from /Literature/summary")
        return [EntrezSummaryResult.model_validate(item) for item in data]

    def get_metadata(self, pmc_id: str) -> ArticleMetadataResult:
        if not pmc_id or not pmc_id.strip():
            raise ValueError("pmcId cannot be null or empty.")
        data = self._get_json(
            "/Literature/metadata",
            params={"pmcId": pmc_id.strip()},
            action="getArticleMetadata",
        )
        return ArticleMetadataResult.model_validate(data)

    def get_abstract(self, pmc_id: str) -> ArticleAbstract:
        if not pmc_id or not pmc_id.strip():
            raise ValueError("pmcId cannot be null or empty.")
        data = self._get_json(
            "/Literature/abstract",
            params={"pmcId": pmc_id.strip()},
            action="getLiteratureAbstract",
        )
        return ArticleAbstract.model_validate(data)

    def get_abstracts_batch(self, pmc_ids: Sequence[str]) -> LiteratureAbstractBatchResult:
        cleaned = [p.strip() for p in pmc_ids if p and str(p).strip()]
        if not cleaned:
            raise ValueError("At least one pmcId is required.")
        data = self._post_json(
            "/Literature/abstracts/batch",
            payload={"pmcIds": cleaned},
            action="getLiteratureAbstractsBatch",
        )
        return LiteratureAbstractBatchResult.model_validate(data)

    def get_download_link(self, pmc_id: str) -> LiteratureDownloadLinkResult:
        if not pmc_id or not pmc_id.strip():
            raise ValueError("pmcId cannot be null or empty.")
        data = self._get_json(
            "/Literature/download",
            params={"pmcId": pmc_id.strip()},
            action="getDownloadLink",
        )
        return LiteratureDownloadLinkResult.model_validate(data)

    def find_candidates(
        self,
        query: str,
        *,
        max_candidates: int | None = None,
        require_pmc_id: bool | None = None,
        require_open_access_link: bool | None = None,
    ) -> LiteratureCandidatesResult:
        """Ranked OA-ingestible candidates (preferred over manual search chaining)."""
        if not query or not query.strip():
            raise ValueError("Query cannot be null or empty.")
        payload: dict[str, Any] = {
            "query": query.strip(),
            "maxCandidates": max_candidates if max_candidates is not None else 0,
        }
        if require_pmc_id is not None:
            payload["requirePmcId"] = require_pmc_id
        if require_open_access_link is not None:
            payload["requireOpenAccessLink"] = require_open_access_link
        data = self._post_json(
            "/Literature/candidates",
            payload=payload,
            action="findLiteratureCandidates",
        )
        return LiteratureCandidatesResult.model_validate(data)

    # --- Literature: ingest -------------------------------------------------

    def request_ingest(
        self,
        items: Sequence[LiteratureIngestItem | Mapping[str, Any]],
        *,
        correlation_id: str | None = None,
        query_preview: str | None = None,
        idempotency_key: str | None = None,
    ) -> LiteratureIngestResponse:
        """
        Enqueue OA papers into the BioAnalyzer download + processing pipeline.

        Does not send requestedBy — server derives identity from auth.
        """
        if not items:
            raise ValueError("At least one ingest item is required.")

        payload_items = [literature_ingest_item_to_payload(item) for item in items]
        key = (idempotency_key or "").strip() or build_ingest_idempotency_key(
            query_preview, items
        )
        body: dict[str, Any] = {
            "items": payload_items,
            "idempotencyKey": key,
        }
        if correlation_id:
            body["correlationId"] = correlation_id
        if query_preview:
            body["queryPreview"] = query_preview

        data = self._post_json(
            "/Literature/ingest",
            payload=body,
            action="requestLiteratureIngest",
            extra_headers={"Idempotency-Key": key},
        )
        return LiteratureIngestResponse.model_validate(data)

    def get_ingest_job_status(self, job_id: str) -> LiteratureIngestJobStatusResult:
        if not job_id or not job_id.strip():
            raise ValueError("jobId is required.")
        data = self._get_json(
            f"/Literature/ingest/{job_id.strip()}",
            action="getIngestJobStatus",
        )
        return LiteratureIngestJobStatusResult.model_validate(data)

    # --- Literature: storage ------------------------------------------------

    def view_downloads(self) -> LiteratureDownloadList:
        data = self._get_json("/Literature/downloads/view", action="viewDownloads")
        return LiteratureDownloadList.model_validate(data)

    def download_file(self, file_name: str) -> bytes:
        if not file_name or not file_name.strip():
            raise ValueError("fileName is required.")
        path = f"/Literature/downloads/{file_name.strip()}"
        response = self._client.get(path, headers=self._auth_headers())
        self._raise_for_status(response, "downloadFile")
        return response.content

    # --- Chat / graph-RAG ---------------------------------------------------

    def query_graph(self, query: str) -> ChatQueryResult:
        """
        Structured graph-RAG query for agent tools.

        Prefers POST /Chat/query; falls back to GET /Chat/evidence.
        """
        if not query or not query.strip():
            raise ValueError("Query cannot be null or empty.")
        trimmed = query.strip()

        headers = self._auth_headers({"Content-Type": "application/json"})
        post_response = self._client.post(
            "/Chat/query",
            json={"query": trimmed},
            headers=headers,
        )
        if post_response.is_success:
            return ChatQueryResult.model_validate(post_response.json())

        if post_response.status_code not in (404, 405):
            self._raise_for_status(post_response, "queryGraph")

        _logger.warning(
            "POST /Chat/query returned %s; falling back to GET /Chat/evidence",
            post_response.status_code,
        )
        data = self._get_json(
            "/Chat/evidence",
            params={"query": trimmed},
            action="queryGraph",
        )
        return ChatQueryResult.model_validate(data)

    def ask_graph(self, query: str) -> ChatQueryResult:
        """Alias used by agent tools."""
        return self.query_graph(query)

    def stream_chat(self, query: str) -> Iterable[str]:
        """
        Yield SSE data payloads from GET /Chat/stream.

        Stops when the stream ends or a [DONE] payload is received.
        """
        if not query or not query.strip():
            raise ValueError("Query cannot be null or empty.")

        with self._client.stream(
            "GET",
            "/Chat/stream",
            params={"query": query.strip()},
            headers=self._auth_headers(),
        ) as response:
            self._raise_for_status(response, "streamChat")
            for line in response.iter_lines():
                if not line or not line.startswith("data:"):
                    continue
                payload = line[5:].strip()
                if payload == "[DONE]":
                    break
                yield payload
