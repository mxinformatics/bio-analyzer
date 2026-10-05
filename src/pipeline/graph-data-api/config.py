from __future__ import annotations

import os
from dataclasses import dataclass

from pipeline_common.azure_helpers import get_azure_credential


@dataclass(frozen=True)
class GraphDataApiConfig:
    require_api_key: bool
    graph_data_api_key: str
    graph_query_alpha: float
    neo4j_uri: str
    neo4j_database: str
    neo4j_username: str
    neo4j_password: str
    neo4j_vector_index: str
    neo4j_text_index: str
    neo4j_vector_dimensions: int
    key_vault_url: str
    key_vault_username_secret_name: str
    key_vault_password_secret_name: str
    azure_storage_account: str
    azure_storage_embedding_container: str
    azure_managed_identity_client_id: str
    foundry_ai_endpoint: str
    foundry_ai_embedding_deployment: str
    foundry_ai_chat_deployment: str
    topic_extraction_max_chunks: int
    topic_extraction_max_chunk_chars: int
    topic_extraction_max_topics: int
    default_top_k: int
    max_query_results: int

    @staticmethod
    def from_env() -> "GraphDataApiConfig":
        key_vault_url = os.getenv("KEY_VAULT_URL", "").strip()
        key_vault_username_secret_name = os.getenv(
            "NEO4J_USERNAME_SECRET_NAME",
            "GraphDatabase--Username",
        ).strip()
        key_vault_password_secret_name = os.getenv(
            "NEO4J_PASSWORD_SECRET_NAME",
            "GraphDatabase--Password",
        ).strip()
        managed_identity_client_id = os.getenv("AZURE_MANAGED_IDENTITY_CLIENT_ID", "").strip()

        if not key_vault_url:
            raise ValueError("KEY_VAULT_URL must be configured for Neo4j credentials.")
        neo4j_username, neo4j_password = _load_neo4j_credentials_from_key_vault(
            key_vault_url=key_vault_url,
            username_secret_name=key_vault_username_secret_name,
            password_secret_name=key_vault_password_secret_name,
            managed_identity_client_id=managed_identity_client_id or None,
        )
        if not neo4j_username or not neo4j_password:
            raise ValueError(
                "Neo4j credentials loaded from Key Vault are empty. "
                "Check KEY_VAULT_URL and Neo4j secret names."
            )
        return GraphDataApiConfig(
            require_api_key=os.getenv("REQUIRE_API_KEY", "false").lower() in ("1", "true", "yes"),
            graph_data_api_key=os.getenv("GRAPH_DATA_API_KEY", ""),
            graph_query_alpha=min(1.0, max(0.0, float(os.getenv("GRAPH_QUERY_ALPHA", "0.7")))),
            neo4j_uri=os.getenv("NEO4J_URI", ""),
            neo4j_database=os.getenv("NEO4J_DATABASE", "neo4j"),
            neo4j_username=neo4j_username,
            neo4j_password=neo4j_password,
            neo4j_vector_index=os.getenv("NEO4J_VECTOR_INDEX", "chunk_embedding_index"),
            neo4j_text_index=os.getenv("NEO4J_TEXT_INDEX", "chunk_text_index"),
            neo4j_vector_dimensions=max(64, int(os.getenv("NEO4J_VECTOR_DIMENSIONS", "512"))),
            key_vault_url=key_vault_url,
            key_vault_username_secret_name=key_vault_username_secret_name,
            key_vault_password_secret_name=key_vault_password_secret_name,
            azure_storage_account=os.getenv("AZURE_STORAGE_ACCOUNT", ""),
            azure_storage_embedding_container=os.getenv(
                "AZURE_STORAGE_EMBEDDING_CONTAINER",
                "pipeline-embedded-chunks",
            ),
            azure_managed_identity_client_id=managed_identity_client_id,
            foundry_ai_endpoint=os.getenv("FOUNDRY_AI_ENDPOINT", ""),
            foundry_ai_embedding_deployment=os.getenv("FOUNDRY_AI_EMBEDDING_DEPLOYMENT", ""),
            foundry_ai_chat_deployment=os.getenv("FOUNDRY_AI_CHAT_DEPLOYMENT", ""),
            topic_extraction_max_chunks=max(1, int(os.getenv("TOPIC_EXTRACTION_MAX_CHUNKS", "12"))),
            topic_extraction_max_chunk_chars=max(200, int(os.getenv("TOPIC_EXTRACTION_MAX_CHUNK_CHARS", "1200"))),
            topic_extraction_max_topics=max(1, int(os.getenv("TOPIC_EXTRACTION_MAX_TOPICS", "12"))),
            default_top_k=max(1, int(os.getenv("DEFAULT_TOP_K", "5"))),
            max_query_results=max(1, int(os.getenv("MAX_QUERY_RESULTS", "100"))),
        )


def _load_neo4j_credentials_from_key_vault(
    key_vault_url: str,
    username_secret_name: str,
    password_secret_name: str,
    managed_identity_client_id: str | None,
) -> tuple[str, str]:
    try:
        from azure.keyvault.secrets import SecretClient
    except ImportError as ex:  # pragma: no cover - optional dependency
        raise RuntimeError(
            "azure-keyvault-secrets package is required for Key Vault Neo4j credentials. "
            "Install dependencies from src/pipeline/requirements.txt."
        ) from ex

    credential = get_azure_credential(managed_identity_client_id)
    secret_client = SecretClient(vault_url=key_vault_url, credential=credential)
    username = secret_client.get_secret(username_secret_name).value
    password = secret_client.get_secret(password_secret_name).value
    return username or "", password or ""
