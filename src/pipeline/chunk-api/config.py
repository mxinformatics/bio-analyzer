from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class ChunkApiConfig:
    require_api_key: bool
    chunk_api_key: str
    chunk_size: int
    chunk_overlap: int
    max_source_bytes: int
    azure_storage_account: str
    azure_storage_chunks_container: str
    azure_source_container: str
    azure_managed_identity_client_id: str

    @staticmethod
    def from_env() -> "ChunkApiConfig":
        return ChunkApiConfig(
            require_api_key=os.getenv("REQUIRE_API_KEY", "false").lower() in ("1", "true", "yes"),
            chunk_api_key=os.getenv("CHUNK_API_KEY", ""),
            chunk_size=max(200, int(os.getenv("DEFAULT_CHUNK_SIZE", "1600"))),
            chunk_overlap=max(0, int(os.getenv("DEFAULT_CHUNK_OVERLAP", "250"))),
            max_source_bytes=max(1024, int(os.getenv("MAX_SOURCE_BYTES", str(1024 * 1024 * 40)))),
            azure_storage_account=os.getenv("AZURE_STORAGE_ACCOUNT", ""),
            azure_storage_chunks_container=os.getenv("AZURE_STORAGE_CHUNKS_CONTAINER", "pipeline-chunks"),
            azure_source_container=os.getenv("AZURE_STORAGE_SOURCE_CONTAINER", ""),
            azure_managed_identity_client_id=os.getenv("AZURE_MANAGED_IDENTITY_CLIENT_ID", ""),
        )
