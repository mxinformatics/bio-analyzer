from __future__ import annotations

import json

from pipeline_common import AzureBlobStorageClient, ChunkData, EmbeddedChunkData


class EmbedArtifactStore:
    def __init__(
        self,
        blob_client: AzureBlobStorageClient,
        chunks_container: str,
        embedded_chunks_container: str,
    ) -> None:
        if not chunks_container:
            raise ValueError("AZURE_STORAGE_CHUNKS_CONTAINER must be configured for embed-api.")
        if not embedded_chunks_container:
            raise ValueError("AZURE_STORAGE_EMBEDDING_CONTAINER must be configured for embed-api.")

        self._blob_client = blob_client
        self._chunks_container = chunks_container
        self._embedded_chunks_container = embedded_chunks_container
        self._blob_client.ensure_containers([chunks_container, embedded_chunks_container])

    @property
    def backend_name(self) -> str:
        return self._blob_client.backend_name

    @property
    def location_descriptor(self) -> str:
        return self._blob_client.location_descriptor

    def readiness_check(self) -> tuple[bool, str]:
        return self._blob_client.readiness_check([self._chunks_container, self._embedded_chunks_container])

    def read_chunks(self, document_id: str) -> list[ChunkData]:
        prefix = f"{document_id}/chunks/"
        chunk_blobs = self._blob_client.list_blob_names(
            container_name=self._chunks_container,
            prefix=prefix,
            suffix=".json",
        )
        chunks: list[ChunkData] = []
        for blob_name in chunk_blobs:
            payload = self._blob_client.get_json(container_name=self._chunks_container, blob_name=blob_name)
            chunks.append(ChunkData.model_validate(payload))
        chunks.sort(key=lambda value: value.chunk_index)
        return chunks

    def write_embedded_chunks(self, document_id: str, chunks: list[EmbeddedChunkData]) -> list[str]:
        written_blobs: list[str] = []
        for chunk in chunks:
            blob_name = f"{document_id}/chunks/chunk-{chunk.chunk_index:05d}.json"
            payload = json.loads(chunk.model_dump_json(by_alias=True, exclude_none=True))
            self._blob_client.put_json(
                container_name=self._embedded_chunks_container,
                blob_name=blob_name,
                payload=payload,
            )
            written_blobs.append(blob_name)
        return written_blobs
