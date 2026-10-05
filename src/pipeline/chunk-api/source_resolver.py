from __future__ import annotations

import asyncio
from pathlib import Path

from models import ChunkDocumentRequest


class SourceResolver:
    def __init__(
        self,
        max_source_bytes: int,
        storage_account: str | None = None,
        source_container: str | None = None,
        managed_identity_client_id: str | None = None,
    ) -> None:
        self._max_source_bytes = max_source_bytes
        self._storage_account = (storage_account or "").strip()
        self._source_container = (source_container or "").strip()
        self._managed_identity_client_id = (managed_identity_client_id or "").strip() or None
        self._blob_service_client = None

        if not self._storage_account:
            raise ValueError("AZURE_STORAGE_ACCOUNT must be configured for chunk-api source resolution.")
        if not self._source_container:
            raise ValueError("AZURE_STORAGE_SOURCE_CONTAINER must be configured for chunk-api source resolution.")

    async def resolve_content(
        self,
        request: ChunkDocumentRequest,
    ) -> tuple[bytes, str, str, str]:
        safe_file_name = Path((request.file_name or "").strip()).name
        if not safe_file_name:
            raise ValueError("file_name is required to resolve source content.")

        content = await asyncio.to_thread(self._download_from_blob_storage, safe_file_name)
        if len(content) > self._max_source_bytes:
            raise ValueError(f"Source exceeds max size {self._max_source_bytes} bytes")
        if not content:
            raise ValueError("Source content was empty")

        source_type = self._infer_source_type_from_file_name(safe_file_name)
        return content, source_type, safe_file_name, ""

    @staticmethod
    def _infer_source_type_from_file_name(file_name: str) -> str:
        lowered = (file_name or "").lower()
        if lowered.endswith((".xml", ".nxml")):
            return "jats_xml"
        return "pdf"

    def _download_from_blob_storage(self, file_name: str) -> bytes:
        blob_service_client = self._get_blob_service_client()
        blob_client = blob_service_client.get_blob_client(container=self._source_container, blob=file_name)
        if not blob_client.exists():
            raise FileNotFoundError(
                f"Source file '{file_name}' was not found in Azure container '{self._source_container}'."
            )
        return blob_client.download_blob().readall()

    def _get_blob_service_client(self):
        if self._blob_service_client is not None:
            return self._blob_service_client

        try:
            from azure.storage.blob import BlobServiceClient
        except ImportError as ex:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "azure-storage-blob is required for Azure source download. "
                "Install dependencies from src/pipeline/requirements.txt."
            ) from ex

        try:
            from pipeline_common.azure_helpers import get_azure_credential
        except ImportError as ex:
            raise RuntimeError("pipeline_common.azure_helpers could not be imported.") from ex

        credential = get_azure_credential(self._managed_identity_client_id)
        account_url = f"https://{self._storage_account}.blob.core.windows.net"
        self._blob_service_client = BlobServiceClient(account_url=account_url, credential=credential)
        return self._blob_service_client

    def readiness_check(self) -> tuple[bool, str]:
        try:
            blob_service_client = self._get_blob_service_client()
            container_client = blob_service_client.get_container_client(self._source_container)
            container_client.get_container_properties()
            return True, (
                "Azure source container is ready: "
                f"{self._storage_account}/{self._source_container}"
            )
        except Exception as ex:  # noqa: BLE001
            return False, f"Azure source readiness failed: {ex}"
