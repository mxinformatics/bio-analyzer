from __future__ import annotations

import json

from pipeline_common import AzureBlobStorageClient, ChunkData


class ChunkArtifactStore:
    def __init__(self, blob_client: AzureBlobStorageClient, chunks_container: str) -> None:
        if not chunks_container:
            raise ValueError("AZURE_STORAGE_CHUNKS_CONTAINER must be configured for chunk-api.")
        self._blob_client = blob_client
        self._chunks_container = chunks_container
        self._blob_client.ensure_container(chunks_container)

    @property
    def backend_name(self) -> str:
        return self._blob_client.backend_name

    @property
    def location_descriptor(self) -> str:
        return self._blob_client.location_descriptor

    def readiness_check(self) -> tuple[bool, str]:
        return self._blob_client.readiness_check([self._chunks_container])

    def write_chunks(self, document_id: str, chunks: list[ChunkData]) -> list[str]:
        written_blobs: list[str] = []
        for chunk in chunks:
            blob_name = f"{document_id}/chunks/chunk-{chunk.chunk_index:05d}.json"
            payload = json.loads(chunk.model_dump_json(by_alias=True, exclude_none=True))
            self._blob_client.put_json(
                container_name=self._chunks_container,
                blob_name=blob_name,
                payload=payload,
            )
            written_blobs.append(blob_name)
        return written_blobs
