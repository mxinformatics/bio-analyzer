from __future__ import annotations

import json
import threading
from typing import Any

from .azure_helpers import get_azure_credential


class AzureBlobStorageClient:
    """
    Generic Azure Blob Storage client.
    This class only exposes transport-level operations and does not encode
    pipeline artifact semantics (chunks, embeddings, graphs, etc).
    """

    def __init__(
        self,
        storage_account: str,
        managed_identity_client_id: str | None = None,
    ) -> None:
        if not storage_account:
            raise ValueError("AZURE_STORAGE_ACCOUNT must be configured for Azure Blob storage.")

        self._storage_account = storage_account
        self._lock = threading.Lock()
        self._account_url = f"https://{storage_account}.blob.core.windows.net"

        try:
            from azure.storage.blob import BlobServiceClient
        except ImportError as ex:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "azure-storage-blob is required for Azure Blob storage access. "
                "Install dependencies from src/pipeline/requirements.txt."
            ) from ex

        credential = get_azure_credential(managed_identity_client_id)
        self._blob_service_client = BlobServiceClient(account_url=self._account_url, credential=credential)

    @property
    def backend_name(self) -> str:
        return "azure_blob"

    @property
    def location_descriptor(self) -> str:
        return self._account_url

    def ensure_container(self, container_name: str) -> None:
        _validate_container_name(container_name)
        container_client = self._blob_service_client.get_container_client(container_name)
        try:
            container_client.create_container()
        except Exception:  # noqa: BLE001
            return

    def ensure_containers(self, container_names: list[str]) -> None:
        for container_name in dict.fromkeys(container_names):
            self.ensure_container(container_name)

    def put_bytes(
        self,
        container_name: str,
        blob_name: str,
        payload: bytes,
        content_type: str | None = None,
    ) -> None:
        _validate_container_name(container_name)
        _validate_blob_name(blob_name)
        try:
            from azure.storage.blob import ContentSettings
        except ImportError as ex:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "azure-storage-blob is required for Azure Blob storage access. "
                "Install dependencies from src/pipeline/requirements.txt."
            ) from ex

        blob_client = self._blob_service_client.get_blob_client(container=container_name, blob=blob_name)
        with self._lock:
            blob_client.upload_blob(
                data=payload,
                overwrite=True,
                content_settings=ContentSettings(content_type=content_type or "application/octet-stream"),
            )

    def get_bytes(self, container_name: str, blob_name: str) -> bytes:
        _validate_container_name(container_name)
        _validate_blob_name(blob_name)
        blob_client = self._blob_service_client.get_blob_client(container=container_name, blob=blob_name)
        return blob_client.download_blob().readall()

    def put_json(self, container_name: str, blob_name: str, payload: dict[str, Any]) -> None:
        self.put_bytes(
            container_name=container_name,
            blob_name=blob_name,
            payload=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
            content_type="application/json",
        )

    def get_json(self, container_name: str, blob_name: str) -> dict[str, Any]:
        raw = self.get_bytes(container_name=container_name, blob_name=blob_name)
        decoded = raw.decode("utf-8")
        return json.loads(decoded)

    def list_blob_names(
        self,
        container_name: str,
        prefix: str = "",
        suffix: str | None = None,
    ) -> list[str]:
        _validate_container_name(container_name)
        container_client = self._blob_service_client.get_container_client(container_name)
        names = [blob.name for blob in container_client.list_blobs(name_starts_with=prefix or "")]
        if suffix:
            names = [name for name in names if name.endswith(suffix)]
        names.sort()
        return names

    def blob_exists(self, container_name: str, blob_name: str) -> bool:
        _validate_container_name(container_name)
        _validate_blob_name(blob_name)
        blob_client = self._blob_service_client.get_blob_client(container=container_name, blob=blob_name)
        return bool(blob_client.exists())

    def readiness_check(self, container_names: list[str]) -> tuple[bool, str]:
        try:
            for container_name in container_names:
                _validate_container_name(container_name)
                container_client = self._blob_service_client.get_container_client(container_name)
                container_client.get_container_properties()
            return True, f"Azure Blob storage is ready: {self._account_url}"
        except Exception as ex:  # noqa: BLE001
            return False, f"Azure Blob readiness failed: {ex}"


def _validate_container_name(container_name: str) -> None:
    if not container_name:
        raise ValueError("Container name must be non-empty.")


def _validate_blob_name(blob_name: str) -> None:
    if not blob_name:
        raise ValueError("Blob name must be non-empty.")
