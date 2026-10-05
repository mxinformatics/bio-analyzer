from __future__ import annotations
import functools
import http.server

import importlib.util
import os
import pathlib
import sys
import tempfile
import threading
import unittest
import uuid
try:
    from fastapi.testclient import TestClient
except Exception:  # noqa: BLE001
    TestClient = None

_PIPELINE_ROOT = pathlib.Path(__file__).resolve().parents[1]
_RUN_AZURE_ENDPOINT_E2E = os.getenv("RUN_PIPELINE_AZURE_ENDPOINT_E2E", "false").lower() in (
    "1",
    "true",
    "yes",
)
_HAS_AZURE_NEO4J_E2E_ENV = all(
    [
        os.getenv("AZURE_STORAGE_ACCOUNT"),
        os.getenv("AZURE_STORAGE_CHUNKS_CONTAINER"),
        os.getenv("AZURE_STORAGE_EMBEDDING_CONTAINER"),
        os.getenv("AZURE_STORAGE_SOURCE_CONTAINER"),
        os.getenv("FOUNDRY_AI_ENDPOINT"),
        os.getenv("FOUNDRY_AI_EMBEDDING_DEPLOYMENT"),
        os.getenv("NEO4J_URI"),
        os.getenv("KEY_VAULT_URL"),
    ]
)

_MODULES_TO_CLEAR = (
    "config",
    "models",
    "main",
    "embedder",
    "graph_repository",
    "query_engine",
    "source_resolver",
    "text_processing",
)


def _clear_transient_service_modules() -> None:
    for module_name in _MODULES_TO_CLEAR:
        sys.modules.pop(module_name, None)


def _load_service_main(service_name: str):
    service_path = _PIPELINE_ROOT / service_name
    main_path = service_path / "main.py"
    if not main_path.exists():
        raise RuntimeError(f"Service main.py not found: {main_path}")

    _clear_transient_service_modules()
    sys.path.insert(0, str(_PIPELINE_ROOT / "pipeline-common"))
    sys.path.insert(0, str(service_path))
    try:
        module_name = f"{service_name.replace('-', '_')}_main_{uuid.uuid4().hex}"
        spec = importlib.util.spec_from_file_location(module_name, main_path)
        if spec is None or spec.loader is None:
            raise RuntimeError(f"Unable to load module from {main_path}")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module
    finally:
        for path_value in (str(service_path), str(_PIPELINE_ROOT / "pipeline-common")):
            if path_value in sys.path:
                sys.path.remove(path_value)


class PipelineEndpointTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_dir = tempfile.TemporaryDirectory()
        self._workspace = pathlib.Path(self._temp_dir.name)
        self._source_root = self._workspace / "source-documents"
        self._source_root.mkdir(parents=True, exist_ok=True)

        self._doc_id = "PMC-E2E-0001"
        self._source_file_name = "pmc-e2e-0001.txt"
        source_content = (
            "BRCA1 gene expression is elevated in breast tissue samples. "
            "TP53 pathway analysis supports downstream biomarker interpretation. "
            "This sentence is repeated to ensure enough tokens for chunk ranking. "
            * 30
        )
        (self._source_root / self._source_file_name).write_text(source_content, encoding="utf-8")
        handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(self._source_root))
        self._source_server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
        self._source_server_thread = threading.Thread(target=self._source_server.serve_forever, daemon=True)
        self._source_server_thread.start()
        self._source_url = (
            f"http://127.0.0.1:{self._source_server.server_address[1]}/{self._source_file_name}"
        )
        if _RUN_AZURE_ENDPOINT_E2E and _HAS_AZURE_NEO4J_E2E_ENV:
            self._upload_source_blob(self._source_file_name, source_content.encode("utf-8"))

        self._env_overrides = {
            "REQUIRE_API_KEY": "false",
            "AZURE_STORAGE_ACCOUNT": os.getenv("AZURE_STORAGE_ACCOUNT", ""),
            "AZURE_STORAGE_CHUNKS_CONTAINER": os.getenv("AZURE_STORAGE_CHUNKS_CONTAINER", "pipeline-chunks"),
            "AZURE_STORAGE_EMBEDDING_CONTAINER": os.getenv(
                "AZURE_STORAGE_EMBEDDING_CONTAINER",
                "pipeline-embedded-chunks",
            ),
            "AZURE_STORAGE_SOURCE_CONTAINER": os.getenv("AZURE_STORAGE_SOURCE_CONTAINER", "pipeline-source"),
            "NEO4J_URI": os.getenv("NEO4J_URI", ""),
            "NEO4J_DATABASE": os.getenv("NEO4J_DATABASE", "neo4j"),
            "KEY_VAULT_URL": os.getenv("KEY_VAULT_URL", ""),
            "NEO4J_USERNAME_SECRET_NAME": os.getenv("NEO4J_USERNAME_SECRET_NAME", "GraphDatabase--Username"),
            "NEO4J_PASSWORD_SECRET_NAME": os.getenv("NEO4J_PASSWORD_SECRET_NAME", "GraphDatabase--Password"),
            "GRAPH_QUERY_ALPHA": "0.7",
            "DEFAULT_CHUNK_SIZE": "400",
            "DEFAULT_CHUNK_OVERLAP": "80",
            "MAX_CHUNKS_PER_REQUEST": "10000",
        }
        self._original_env: dict[str, str | None] = {}
        for key, value in self._env_overrides.items():
            self._original_env[key] = os.environ.get(key)
            os.environ[key] = value

    def tearDown(self) -> None:
        for key, original_value in self._original_env.items():
            if original_value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = original_value
        self._source_server.shutdown()
        self._source_server.server_close()
        self._source_server_thread.join(timeout=2.0)
        _clear_transient_service_modules()
        self._temp_dir.cleanup()

    def _upload_source_blob(self, file_name: str, payload: bytes) -> None:
        try:
            from azure.identity import DefaultAzureCredential
            from azure.storage.blob import BlobServiceClient
        except ImportError:
            self.skipTest("azure-identity and azure-storage-blob are required for Azure endpoint e2e.")

        storage_account = os.getenv("AZURE_STORAGE_ACCOUNT", "").strip()
        source_container = os.getenv("AZURE_STORAGE_SOURCE_CONTAINER", "").strip()
        if not storage_account or not source_container:
            self.skipTest("AZURE_STORAGE_ACCOUNT and AZURE_STORAGE_SOURCE_CONTAINER are required for Azure endpoint e2e.")

        managed_identity_client_id = os.getenv("AZURE_MANAGED_IDENTITY_CLIENT_ID", "").strip()
        credential_kwargs: dict[str, str] = {}
        if managed_identity_client_id:
            credential_kwargs["managed_identity_client_id"] = managed_identity_client_id
        credential = DefaultAzureCredential(**credential_kwargs)

        account_url = f"https://{storage_account}.blob.core.windows.net"
        blob_service_client = BlobServiceClient(account_url=account_url, credential=credential)
        container_client = blob_service_client.get_container_client(source_container)
        try:
            container_client.create_container()
        except Exception:  # noqa: BLE001
            pass
        container_client.upload_blob(name=file_name, data=payload, overwrite=True)

    @unittest.skipUnless(
        importlib.util.find_spec("fastapi")
        and TestClient is not None
        and _RUN_AZURE_ENDPOINT_E2E
        and _HAS_AZURE_NEO4J_E2E_ENV,
        (
            "fastapi test dependencies, RUN_PIPELINE_AZURE_ENDPOINT_E2E=true, and "
            "Azure/Neo4j Key Vault/Foundry env vars are required"
        ),
    )
    def test_pipeline_endpoints_azure_flow(self) -> None:
        chunk_main = _load_service_main("chunk-api")
        embed_main = _load_service_main("embed-api")
        graph_main = _load_service_main("graph-data-api")

        with (
            TestClient(chunk_main.app) as chunk_client,
            TestClient(embed_main.app) as embed_client,
            TestClient(graph_main.app) as graph_client,
        ):
            chunk_ready = chunk_client.get("/ready")
            embed_ready = embed_client.get("/ready")
            graph_ready = graph_client.get("/ready")
            self.assertEqual(200, chunk_ready.status_code, chunk_ready.text)
            self.assertEqual(200, embed_ready.status_code, embed_ready.text)
            self.assertEqual(200, graph_ready.status_code, graph_ready.text)

            chunk_request = {
                "document_name": "Pipeline Endpoint E2E Document",
                "file_name": self._source_file_name,
                "document_id": self._doc_id,
                "metadata": {
                    "source_system_doc_id": self._doc_id,
                    "doc_type": "biomedical_research_article",
                    "original_file_name": self._source_file_name,
                },
            }
            correlation_id = "pipeline-e2e-correlation-id"

            chunk_response = chunk_client.post(
                "/chunk",
                json=chunk_request,
                headers={"X-Correlation-Id": correlation_id},
            )
            self.assertEqual(200, chunk_response.status_code, chunk_response.text)
            self.assertEqual(correlation_id, chunk_response.headers.get("X-Correlation-Id"))
            chunk_payload = chunk_response.json()
            self.assertGreater(chunk_payload["total_chunks"], 0)
            self.assertEqual(self._doc_id, chunk_payload["document_id"])

            embed_request = {
                "document_id": self._doc_id,
            }
            embed_response = embed_client.post(
                "/embed",
                json=embed_request,
                headers={"X-Correlation-Id": correlation_id},
            )
            self.assertEqual(200, embed_response.status_code, embed_response.text)
            self.assertEqual(correlation_id, embed_response.headers.get("X-Correlation-Id"))
            embed_payload = embed_response.json()
            self.assertEqual(self._doc_id, embed_payload["document_id"])
            self.assertEqual(chunk_payload["total_chunks"], embed_payload["total_chunks"])

            graph_build_response = graph_client.post(
                "/build-graph",
                json={"document_id": self._doc_id},
                headers={"X-Correlation-Id": correlation_id},
            )
            self.assertEqual(200, graph_build_response.status_code, graph_build_response.text)
            self.assertEqual(correlation_id, graph_build_response.headers.get("X-Correlation-Id"))
            graph_build_payload = graph_build_response.json()
            self.assertEqual(self._doc_id, graph_build_payload["document_id"])
            self.assertGreater(graph_build_payload["nodes_created"], 0)

            graph_query_response = graph_client.post(
                "/query",
                json={"query": "BRCA1 expression pathway", "top_k": 5},
                headers={"X-Correlation-Id": correlation_id},
            )
            self.assertEqual(200, graph_query_response.status_code, graph_query_response.text)
            self.assertEqual(correlation_id, graph_query_response.headers.get("X-Correlation-Id"))
            graph_query_payload = graph_query_response.json()
            self.assertGreaterEqual(graph_query_payload["total_results"], 1)
            self.assertEqual("BRCA1 expression pathway", graph_query_payload["query"])
            top_result = graph_query_payload["results"][0]
            self.assertEqual(self._doc_id, top_result["source_system_doc_id"])
            self.assertIn("expression", top_result["text"].lower())
