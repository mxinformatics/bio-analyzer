from __future__ import annotations

import json
import re
from typing import Any

from pipeline_common import EmbeddedChunkData
from pipeline_common.azure_helpers import get_foundry_token_provider


class AzureOpenAiTopicExtractor:
    def __init__(
        self,
        endpoint: str,
        chat_deployment: str,
        managed_identity_client_id: str | None = None,
        max_chunks: int = 12,
        max_chunk_chars: int = 1200,
        max_topics: int = 12,
    ) -> None:
        self._max_chunks = max(1, max_chunks)
        self._max_chunk_chars = max(200, max_chunk_chars)
        self._max_topics = max(1, max_topics)

        self._is_enabled = bool(endpoint.strip()) and bool(chat_deployment.strip())
        self._disabled_reason = ""
        if not self._is_enabled:
            self._disabled_reason = (
                "Topic extraction disabled because FOUNDRY_AI_ENDPOINT or "
                "FOUNDRY_AI_CHAT_DEPLOYMENT is not configured."
            )
            self._client = None
            self._chat_deployment = ""
            return

        try:
            from openai import OpenAI
        except ImportError as ex:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "openai package is required for topic extraction provider. "
                "Install dependencies from src/pipeline/requirements.txt."
            ) from ex

        credential = get_foundry_token_provider(managed_identity_client_id)
        self._client = OpenAI(
            base_url=f"{endpoint.rstrip('/')}/openai/v1/",
            api_key=credential,
        )
        self._chat_deployment = chat_deployment.strip()

    @property
    def is_enabled(self) -> bool:
        return self._is_enabled

    @property
    def disabled_reason(self) -> str:
        return self._disabled_reason

    def extract_topics(
        self,
        embedded_chunks: list[EmbeddedChunkData],
        publication_types: list[str] | None = None,
    ) -> list[str]:
        if not self._is_enabled or self._client is None:
            return []
        if not embedded_chunks:
            return []

        chunk_context = self._build_chunk_context(embedded_chunks)
        if not chunk_context:
            return []

        publication_type_values = [
            value.strip()
            for value in (publication_types or [])
            if isinstance(value, str) and value.strip()
        ]
        publication_type_block = ", ".join(publication_type_values) if publication_type_values else "None"

        response = self._client.chat.completions.create(
            model=self._chat_deployment,
            temperature=0.0,
            response_format={"type": "json_object"},
            messages=[
                {
                    "role": "system",
                    "content": (
                        "You extract concise biomedical topics from article content. "
                        "Return only JSON in this exact shape: {\"topics\": [\"...\"]}. "
                        "Topics must be noun phrases, specific and content-derived."
                    ),
                },
                {
                    "role": "user",
                    "content": (
                        f"Generate up to {self._max_topics} biomedical topics from the article excerpts below.\n"
                        "Do not include publication types, document genre labels, or study-design labels.\n"
                        f"Publication types to exclude: {publication_type_block}\n\n"
                        f"Article excerpts:\n{chunk_context}\n"
                    ),
                },
            ],
        )
        content = _extract_response_content(response)
        payload = _parse_json_payload(content)
        raw_topics = payload.get("topics", [])
        if not isinstance(raw_topics, list):
            return []

        excluded = {value.casefold() for value in publication_type_values}
        normalized: list[str] = []
        seen: set[str] = set()
        for raw_topic in raw_topics:
            if not isinstance(raw_topic, str):
                continue
            topic = _normalize_topic(raw_topic)
            if not topic:
                continue
            normalized_key = topic.casefold()
            if normalized_key in excluded or normalized_key in seen:
                continue
            seen.add(normalized_key)
            normalized.append(topic)
            if len(normalized) >= self._max_topics:
                break
        return normalized

    def _build_chunk_context(self, embedded_chunks: list[EmbeddedChunkData]) -> str:
        selected_chunks = _select_representative_chunks(embedded_chunks, self._max_chunks)
        lines: list[str] = []
        for chunk in selected_chunks:
            text = " ".join((chunk.content or "").split())
            if not text:
                continue
            if len(text) > self._max_chunk_chars:
                text = f"{text[: self._max_chunk_chars]}..."
            lines.append(f"[chunk {chunk.chunk_index}] {text}")
        return "\n".join(lines)


def _select_representative_chunks(chunks: list[EmbeddedChunkData], max_chunks: int) -> list[EmbeddedChunkData]:
    if len(chunks) <= max_chunks:
        return chunks

    abstract_chunks = [
        chunk
        for chunk in chunks
        if (chunk.metadata.section_type or "").strip().lower() in {"abstract", "summary", "highlights"}
    ]
    selected: list[EmbeddedChunkData] = abstract_chunks[:max_chunks]
    if len(selected) >= max_chunks:
        return selected

    selected_ids = {chunk.chunk_id for chunk in selected}
    remaining = [chunk for chunk in chunks if chunk.chunk_id not in selected_ids]
    selected.extend(remaining[: max_chunks - len(selected)])
    return selected


def _extract_response_content(response: Any) -> str:
    choices = getattr(response, "choices", None) or []
    if not choices:
        return "{}"
    message = getattr(choices[0], "message", None)
    if message is None:
        return "{}"
    content = getattr(message, "content", "")
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        fragments: list[str] = []
        for item in content:
            text_value = getattr(item, "text", None) if item is not None else None
            if isinstance(text_value, str):
                fragments.append(text_value)
        return "\n".join(fragments) if fragments else "{}"
    return "{}"


def _parse_json_payload(content: str) -> dict[str, Any]:
    stripped = content.strip()
    if not stripped:
        return {}
    try:
        payload = json.loads(stripped)
        if isinstance(payload, dict):
            return payload
    except json.JSONDecodeError:
        pass

    match = re.search(r"\{.*\}", stripped, flags=re.DOTALL)
    if not match:
        return {}
    try:
        payload = json.loads(match.group(0))
        return payload if isinstance(payload, dict) else {}
    except json.JSONDecodeError:
        return {}


def _normalize_topic(value: str) -> str:
    compact = re.sub(r"\s+", " ", value).strip(" \t\n\r,;:.")
    if not compact:
        return ""
    if len(compact) > 120:
        compact = compact[:120].rstrip(" ,;:.")
    return compact
