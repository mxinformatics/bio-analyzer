from __future__ import annotations

import logging

import random
import time

from pipeline_common.azure_helpers import get_foundry_token_provider

_logger = logging.getLogger(__name__)


class AzureOpenAiTextEmbedder:
    """
    Azure OpenAI (Foundry v1 endpoint) embedding provider.
    """

    def __init__(
        self,
        endpoint: str,
        embedding_deployment: str,
        managed_identity_client_id: str | None = None,
        batch_size: int = 100,
        max_retries: int = 1,
    ) -> None:
        if not endpoint:
            raise ValueError("FOUNDRY_AI_ENDPOINT must be configured for Foundry embeddings.")
        if not embedding_deployment:
            raise ValueError("FOUNDRY_AI_EMBEDDING_DEPLOYMENT must be configured for Foundry embeddings.")

        try:
            from openai import NotFoundError, OpenAI, RateLimitError
        except ImportError as ex:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "openai package is required for Foundry embedding provider. "
                "Install dependencies from src/pipeline/requirements.txt."
            ) from ex

        self._endpoint = endpoint.rstrip("/")
        self._embedding_deployment = embedding_deployment
        self._batch_size = max(1, batch_size)
        self._max_retries = min(1, max(0, max_retries))
        self._max_attempts = self._max_retries + 1
        self._rate_limit_error = RateLimitError
        self._not_found_error = NotFoundError
        self._dimension: int | None = None
        self._last_readiness_checked_at = 0.0
        self._last_readiness_result: tuple[bool, str] = (False, "Readiness has not been checked yet.")
        self._readiness_cache_seconds = 60.0
        credential = get_foundry_token_provider(managed_identity_client_id)

        self._client = OpenAI(
            base_url=f"{self._endpoint}/openai/v1/",
            api_key=credential,
        )

    @property
    def dimension(self) -> int:
        return self._dimension or 0

    def embed_text(self, text: str) -> list[float]:
        return self._embed_texts_batch([text])[0]

    def embed_texts(self, texts: list[str]) -> list[list[float]]:
        if not texts:
            return []
        embeddings: list[list[float]] = []
        for start_index in range(0, len(texts), self._batch_size):
            batch = texts[start_index : start_index + self._batch_size]
            embeddings.extend(self._embed_texts_batch(batch))
        return embeddings

    def _embed_texts_batch(self, texts: list[str]) -> list[list[float]]:
        last_error: Exception | None = None
        for attempt in range(1, self._max_attempts + 1):
            try:
                response = self._client.embeddings.create(
                    input=texts,
                    model=self._embedding_deployment,
                )
                embeddings = [item.embedding for item in response.data]
                if embeddings and self._dimension is None:
                    self._dimension = len(embeddings[0])
                return embeddings
            except self._not_found_error as ex:
                detail_message = self._build_not_found_detail_message(ex, len(texts), attempt)
                _logger.error(detail_message)
                raise RuntimeError(detail_message) from ex
            except self._rate_limit_error as ex:
                last_error = ex
                error_type, status_code, request_id, error_payload = self._extract_error_diagnostics(ex)
                _logger.warning(
                    "Foundry embeddings request rate-limited. endpoint=%s/openai/v1/ deployment=%s "
                    "batch_size=%s attempt=%s/%s error_type=%s status_code=%s request_id=%s "
                    "error_payload=%s",
                    self._endpoint,
                    self._embedding_deployment,
                    len(texts),
                    attempt,
                    self._max_attempts,
                    error_type,
                    status_code,
                    request_id,
                    error_payload,
                )
                if attempt == self._max_attempts:
                    break
                wait_seconds = (2 ** (attempt - 1)) + random.uniform(0.0, 1.0)
                _logger.info(
                    "Retrying Foundry embeddings after rate limit. endpoint=%s/openai/v1/ deployment=%s "
                    "batch_size=%s next_attempt=%s/%s backoff_seconds=%.2f",
                    self._endpoint,
                    self._embedding_deployment,
                    len(texts),
                    attempt + 1,
                    self._max_attempts,
                    wait_seconds,
                )
                time.sleep(wait_seconds)
            except Exception as ex:  # noqa: BLE001
                last_error = ex
                error_type, status_code, request_id, error_payload = self._extract_error_diagnostics(ex)
                _logger.warning(
                    "Foundry embeddings request failed. endpoint=%s/openai/v1/ deployment=%s "
                    "batch_size=%s attempt=%s/%s error_type=%s status_code=%s request_id=%s "
                    "error_payload=%s",
                    self._endpoint,
                    self._embedding_deployment,
                    len(texts),
                    attempt,
                    self._max_attempts,
                    error_type,
                    status_code,
                    request_id,
                    error_payload,
                )
                if attempt == self._max_attempts:
                    break
                wait_seconds = (2 ** (attempt - 1)) + random.uniform(0.0, 1.0)
                _logger.info(
                    "Retrying Foundry embeddings after failure. endpoint=%s/openai/v1/ deployment=%s "
                    "batch_size=%s next_attempt=%s/%s backoff_seconds=%.2f",
                    self._endpoint,
                    self._embedding_deployment,
                    len(texts),
                    attempt + 1,
                    self._max_attempts,
                    wait_seconds,
                )
                time.sleep(wait_seconds)
        if last_error is None:
            raise RuntimeError(
                f"Embedding request failed after {self._max_attempts} attempts "
                f"({self._max_retries} retry) without an underlying exception."
            )
        error_type, status_code, request_id, error_payload = self._extract_error_diagnostics(last_error)
        raise RuntimeError(
            f"Embedding request failed after {self._max_attempts} attempts "
            f"({self._max_retries} retry): endpoint={self._endpoint}/openai/v1/ "
            f"deployment={self._embedding_deployment} batch_size={len(texts)} "
            f"error_type={error_type} status_code={status_code} request_id={request_id} "
            f"error_payload={error_payload}"
        ) from last_error

    def _build_not_found_detail_message(self, error: Exception, batch_size: int, attempt: int) -> str:
        error_type, status_code, request_id, error_payload = self._extract_error_diagnostics(error)
        return (
            "Foundry embeddings request returned 404 Not Found. "
            f"operation=embeddings.create endpoint={self._endpoint}/openai/v1/ "
            f"deployment={self._embedding_deployment} batch_size={batch_size} "
            f"attempt={attempt}/{self._max_attempts} error_type={error_type} status_code={status_code} "
            f"request_id={request_id} error_payload={error_payload}"
        )

    @classmethod
    def _extract_error_diagnostics(cls, error: Exception) -> tuple[str, str, str, str]:
        return (
            type(error).__name__,
            str(getattr(error, "status_code", None) or "unknown"),
            cls._extract_request_id(error),
            cls._extract_error_payload(error),
        )

    @staticmethod
    def _extract_request_id(error: Exception) -> str:
        direct_request_id = getattr(error, "request_id", None)
        if direct_request_id:
            return str(direct_request_id)
        response = getattr(error, "response", None)
        headers = getattr(response, "headers", None)
        if headers and hasattr(headers, "get"):
            header_request_id = headers.get("x-request-id") or headers.get("request-id")
            if header_request_id:
                return str(header_request_id)
        return "unknown"

    @staticmethod
    def _extract_error_payload(error: Exception) -> str:
        payload = getattr(error, "body", None)
        if payload:
            return str(payload)[:2000]
        response = getattr(error, "response", None)
        response_text = getattr(response, "text", None)
        if response_text:
            return str(response_text)[:2000]
        return str(error)[:2000]

    def readiness_check(self, force_refresh: bool = False) -> tuple[bool, str]:
        current_time = time.time()
        if (
            not force_refresh
            and (current_time - self._last_readiness_checked_at) < self._readiness_cache_seconds
        ):
            return self._last_readiness_result

        try:
            self._embed_texts_batch(["readiness probe"])
            self._last_readiness_result = (True, "Foundry embedding provider is ready.")
        except Exception as ex:  # noqa: BLE001
            self._last_readiness_result = (False, f"Foundry readiness probe failed: {ex}")

        self._last_readiness_checked_at = current_time
        return self._last_readiness_result
