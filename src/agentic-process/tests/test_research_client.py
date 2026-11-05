"""Unit tests for Research API client helpers (no live API required)."""

from __future__ import annotations

import hashlib
import json

import httpx
import pytest

from agentic_process.config import ResearchApiSettings
from agentic_process.research.client import (
    ResearchApiClient,
    build_ingest_idempotency_key,
)
from agentic_process.research.exceptions import ResearchApiHttpError
from agentic_process.research.models import (
    ChatQueryResult,
    ChatQueryStatus,
    EntrezSearchResult,
    LiteratureIngestItem,
)


def test_build_ingest_idempotency_key_is_stable_and_sorted() -> None:
    items_a = [
        {"pmcId": "pmc7788"},
        LiteratureIngestItem(pmcId="PMC1234", title="t"),
    ]
    items_b = [
        LiteratureIngestItem(pmcId="PMC1234"),
        {"pmc_id": "PMC7788"},
    ]
    key_a = build_ingest_idempotency_key("crispr question", items_a)
    key_b = build_ingest_idempotency_key("crispr question", items_b)
    assert key_a == key_b
    expected_material = "crispr question|PMC1234,PMC7788"
    assert key_a == hashlib.sha256(expected_material.encode("utf-8")).hexdigest()


def test_entrez_search_result_parses_camel_case() -> None:
    result = EntrezSearchResult.model_validate(
        {
            "count": "42",
            "retMax": "20",
            "retStart": "0",
            "idList": ["1", "2"],
        }
    )
    assert result.count == "42"
    assert result.ret_max == "20"
    assert result.id_list == ["1", "2"]


def test_chat_query_result_answered_property() -> None:
    answered = ChatQueryResult(status=ChatQueryStatus.ANSWERED, answer="yes")
    missed = ChatQueryResult(status=ChatQueryStatus.NO_RESULTS)
    assert answered.answered is True
    assert missed.answered is False


def test_auth_headers_prefer_bearer_over_api_key() -> None:
    settings = ResearchApiSettings(
        base_url="http://example.test",
        api_key="key-value",
        bearer_token="token-value",
    )

    def handler(request: httpx.Request) -> httpx.Response:
        assert request.headers.get("Authorization") == "Bearer token-value"
        assert "X-Api-Key" not in request.headers
        return httpx.Response(
            200,
            json={"count": "0", "retMax": "0", "retStart": "0", "idList": []},
        )

    http_client = httpx.Client(transport=httpx.MockTransport(handler), base_url=settings.base_url)
    with ResearchApiClient(settings, client=http_client) as client:
        result = client.search_literature("test")
    assert result.id_list == []


def test_search_literature_sends_api_key_and_parses() -> None:
    settings = ResearchApiSettings(base_url="http://example.test", api_key="local-dev-key")

    def handler(request: httpx.Request) -> httpx.Response:
        assert request.headers.get("X-Api-Key") == "local-dev-key"
        assert request.url.params["query"] == "brca1"
        assert request.url.params["startIndex"] == "0"
        return httpx.Response(
            200,
            json={"count": "1", "retMax": "1", "retStart": "0", "idList": ["999"]},
        )

    http_client = httpx.Client(transport=httpx.MockTransport(handler), base_url=settings.base_url)
    with ResearchApiClient(settings, client=http_client) as client:
        result = client.search_literature("brca1")
    assert result.id_list == ["999"]


def test_find_candidates_posts_body() -> None:
    settings = ResearchApiSettings(base_url="http://example.test", api_key="k")

    def handler(request: httpx.Request) -> httpx.Response:
        assert request.method == "POST"
        assert request.url.path.endswith("/Literature/candidates")
        body = json.loads(request.content.decode())
        assert body["query"] == "open access oncology"
        assert body["maxCandidates"] == 2
        return httpx.Response(
            200,
            json={
                "query": "open access oncology",
                "enabled": True,
                "searchHitCount": 10,
                "candidates": [
                    {
                        "pmid": "1",
                        "pmcId": "PMC1",
                        "title": "Paper",
                        "doi": "10.1/x",
                        "pdfLink": "https://example/pdf",
                        "xmlLink": "",
                        "alreadyIngested": False,
                        "rank": 1,
                    }
                ],
                "skippedReasons": [],
            },
        )

    http_client = httpx.Client(transport=httpx.MockTransport(handler), base_url=settings.base_url)
    with ResearchApiClient(settings, client=http_client) as client:
        result = client.find_candidates("open access oncology", max_candidates=2)
    assert len(result.candidates) == 1
    assert result.candidates[0].pmc_id == "PMC1"


def test_request_ingest_sets_idempotency_header() -> None:
    settings = ResearchApiSettings(base_url="http://example.test", api_key="k")
    captured: dict[str, str] = {}

    def handler(request: httpx.Request) -> httpx.Response:
        captured["idempotency"] = request.headers.get("Idempotency-Key", "")
        body = json.loads(request.content.decode())
        assert "requestedBy" not in body
        assert body["idempotencyKey"] == captured["idempotency"]
        return httpx.Response(
            200,
            json={
                "jobId": "job-1",
                "status": "Accepted",
                "enqueued": [],
                "skipped": [],
                "replayed": False,
            },
        )

    http_client = httpx.Client(transport=httpx.MockTransport(handler), base_url=settings.base_url)
    item = LiteratureIngestItem(pmcId="PMC42", title="T")
    with ResearchApiClient(settings, client=http_client) as client:
        result = client.request_ingest([item], query_preview="q")
    assert result.job_id == "job-1"
    assert captured["idempotency"] == build_ingest_idempotency_key("q", [item])


def test_http_error_includes_status() -> None:
    settings = ResearchApiSettings(base_url="http://example.test", api_key="k")

    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(401, text="unauthorized")

    http_client = httpx.Client(transport=httpx.MockTransport(handler), base_url=settings.base_url)
    with (
        ResearchApiClient(settings, client=http_client) as client,
        pytest.raises(ResearchApiHttpError) as exc_info,
    ):
        client.search_literature("x")
    assert exc_info.value.status_code == 401
