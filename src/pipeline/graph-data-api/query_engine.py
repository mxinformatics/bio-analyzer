from __future__ import annotations

import re
from collections import Counter

from models import QueryRequest, ScoredDocumentResult
from pipeline_common import GraphDocument


def rank_chunks(
    query_request: QueryRequest,
    graph_documents: list[GraphDocument],
    max_results: int,
) -> list[ScoredDocumentResult]:
    query_text = query_request.query.strip()
    if not query_text:
        return []

    query_tokens = _tokenize(query_text)
    if not query_tokens:
        return []

    requested_types = _resolve_requested_types(query_request)
    scored_results: list[ScoredDocumentResult] = []

    for graph_document in graph_documents:
        if requested_types and graph_document.doc_type not in requested_types:
            continue

        for chunk in graph_document.chunks:
            chunk_tokens = _tokenize(chunk.content)
            lexical_score = _lexical_score(query_tokens, chunk_tokens)
            if lexical_score <= 0.0:
                continue

            metadata_payload = chunk.metadata.model_dump(by_alias=True, exclude_none=True)
            scored_results.append(
                ScoredDocumentResult(
                    score=round(lexical_score, 6),
                    text=chunk.content,
                    page_number=chunk.page_number,
                    chunk_index=chunk.chunk_index,
                    document_name=chunk.metadata.document_name or "",
                    source_system_doc_id=chunk.metadata.source_system_doc_id or "",
                    document_type=chunk.metadata.doc_type or graph_document.doc_type,
                    metadata=metadata_payload,
                )
            )

    scored_results.sort(key=lambda result: result.score, reverse=True)
    effective_limit = min(query_request.top_k, max_results)
    return scored_results[:effective_limit]


def _resolve_requested_types(query_request: QueryRequest) -> set[str]:
    if query_request.document_types:
        return {value for value in query_request.document_types if value}
    if query_request.document_type:
        return {query_request.document_type}
    return set()


def _tokenize(value: str) -> list[str]:
    return re.findall(r"[a-zA-Z0-9_]+", value.lower())


def _lexical_score(query_tokens: list[str], chunk_tokens: list[str]) -> float:
    chunk_counter = Counter(chunk_tokens)
    overlap = 0.0
    for token in query_tokens:
        if token in chunk_counter:
            overlap += 1.0 + min(2.0, 0.2 * chunk_counter[token])
    return overlap / max(1.0, len(query_tokens))
