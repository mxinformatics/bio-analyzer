from __future__ import annotations

import random
import time

from pipeline_common.azure_helpers import get_foundry_token_provider


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
        max_retries: int = 5,
    ) -> None:
        if not endpoint:
            raise ValueError("FOUNDRY_AI_ENDPOINT must be configured for Foundry embeddings.")
        if not embedding_deployment:
            raise ValueError("FOUNDRY_AI_EMBEDDING_DEPLOYMENT must be configured for Foundry embeddings.")

        try:
            from openai import OpenAI, RateLimitError
        except ImportError as ex:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "openai package is required for Foundry embedding provider. "
                "Install dependencies from src/pipeline/requirements.txt."
            ) from ex

        self._endpoint = endpoint.rstrip("/")
        self._embedding_deployment = embedding_deployment
        self._batch_size = max(1, batch_size)
        self._max_retries = max(1, max_retries)
        self._rate_limit_error = RateLimitError
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
        for attempt in range(1, self._max_retries + 1):
            try:
                response = self._client.embeddings.create(
                    input=texts,
                    model=self._embedding_deployment,
                )
                embeddings = [item.embedding for item in response.data]
                if embeddings and self._dimension is None:
                    self._dimension = len(embeddings[0])
                return embeddings
            except self._rate_limit_error as ex:
                last_error = ex
                if attempt == self._max_retries:
                    break
                wait_seconds = (2 ** (attempt - 1)) + random.uniform(0.0, 1.0)
                time.sleep(wait_seconds)
            except Exception as ex:  # noqa: BLE001
                last_error = ex
                if attempt == self._max_retries:
                    break
                wait_seconds = (2 ** (attempt - 1)) + random.uniform(0.0, 1.0)
                time.sleep(wait_seconds)

        raise RuntimeError(f"Embedding request failed after {self._max_retries} attempts: {last_error}")

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
