from __future__ import annotations

import os
from typing import Optional


def get_azure_credential(managed_identity_client_id: Optional[str] = None):
    """
    Build DefaultAzureCredential with optional user-assigned managed identity.
    """

    try:
        from azure.identity import DefaultAzureCredential
    except ImportError as ex:  # pragma: no cover - optional dependency
        raise RuntimeError(
            "azure-identity is required for Azure-backed pipeline features. "
            "Install dependencies from src/pipeline/requirements.txt."
        ) from ex

    resolved_client_id = managed_identity_client_id or os.getenv("AZURE_MANAGED_IDENTITY_CLIENT_ID")
    kwargs: dict[str, str] = {}
    if resolved_client_id:
        kwargs["managed_identity_client_id"] = resolved_client_id
    return DefaultAzureCredential(**kwargs)


def get_foundry_token_provider(managed_identity_client_id: Optional[str] = None):
    """
    Create a bearer token provider for Microsoft Foundry (Azure OpenAI v1 endpoint).
    """

    try:
        from azure.identity import get_bearer_token_provider
    except ImportError as ex:  # pragma: no cover - optional dependency
        raise RuntimeError(
            "azure-identity is required for managed-identity Foundry authentication. "
            "Install dependencies from src/pipeline/requirements.txt."
        ) from ex

    credential = get_azure_credential(managed_identity_client_id)
    return get_bearer_token_provider(credential, "https://ai.azure.com/.default")
