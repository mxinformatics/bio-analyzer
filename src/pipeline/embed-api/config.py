from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class EmbedApiConfig:
    require_api_key: bool
    embedding_api_key: str
    embedding_batch_size: int
    embedding_max_retries: int
    max_chunks_per_request: int
    azure_storage_account: str
    azure_storage_chunks_container: str
    azure_storage_embedding_container: str
    foundry_ai_endpoint: str
    foundry_ai_embedding_deployment: str
    azure_managed_identity_client_id: str

    @staticmethod
    def from_env() -> "EmbedApiConfig":
        return EmbedApiConfig(
            require_api_key=os.getenv("REQUIRE_API_KEY", "false").lower() in ("1", "true", "yes"),
            embedding_api_key=os.getenv("EMBEDDING_API_KEY", ""),
            embedding_batch_size=max(1, int(os.getenv("EMBEDDING_BATCH_SIZE", "100"))),
            embedding_max_retries=min(1, max(0, int(os.getenv("EMBEDDING_MAX_RETRIES", "1")))),
            max_chunks_per_request=max(1, int(os.getenv("MAX_CHUNKS_PER_REQUEST", "25000"))),
            azure_storage_account=os.getenv("AZURE_STORAGE_ACCOUNT", ""),
            azure_storage_chunks_container=os.getenv("AZURE_STORAGE_CHUNKS_CONTAINER", "pipeline-chunks"),
            azure_storage_embedding_container=os.getenv(
                "AZURE_STORAGE_EMBEDDING_CONTAINER",
                "pipeline-embedded-chunks",
            ),
            foundry_ai_endpoint=os.getenv("FOUNDRY_AI_ENDPOINT", ""),
            foundry_ai_embedding_deployment=os.getenv("FOUNDRY_AI_EMBEDDING_DEPLOYMENT", ""),
            azure_managed_identity_client_id=os.getenv("AZURE_MANAGED_IDENTITY_CLIENT_ID", ""),
        )
