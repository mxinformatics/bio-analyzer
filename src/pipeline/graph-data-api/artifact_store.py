from __future__ import annotations

from pipeline_common import AzureBlobStorageClient, EmbeddedChunkData


class GraphArtifactStore:
    def __init__(self, blob_client: AzureBlobStorageClient, embedded_chunks_container: str) -> None:
        if not embedded_chunks_container:
            raise ValueError("AZURE_STORAGE_EMBEDDING_CONTAINER must be configured for graph-data-api.")

        self._blob_client = blob_client
        self._embedded_chunks_container = embedded_chunks_container
        self._blob_client.ensure_container(embedded_chunks_container)

    @property
    def backend_name(self) -> str:
        return self._blob_client.backend_name

    @property
    def location_descriptor(self) -> str:
        return self._blob_client.location_descriptor

    def readiness_check(self) -> tuple[bool, str]:
        return self._blob_client.readiness_check([self._embedded_chunks_container])

    def read_embedded_chunks(self, document_id: str) -> list[EmbeddedChunkData]:
        prefix = f"{document_id}/chunks/"
        chunk_blobs = self._blob_client.list_blob_names(
            container_name=self._embedded_chunks_container,
            prefix=prefix,
            suffix=".json",
        )
        chunks: list[EmbeddedChunkData] = []
        for blob_name in chunk_blobs:
            payload = self._blob_client.get_json(
                container_name=self._embedded_chunks_container,
                blob_name=blob_name,
            )
            chunks.append(EmbeddedChunkData.model_validate(payload))
        chunks.sort(key=lambda value: value.chunk_index)
        return chunks
