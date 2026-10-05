from __future__ import annotations

import importlib.util
import pathlib
import sys
import unittest

_PIPELINE_ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(_PIPELINE_ROOT / "pipeline-common"))
sys.path.insert(0, str(_PIPELINE_ROOT / "chunk-api"))
sys.path.insert(0, str(_PIPELINE_ROOT / "embed-api"))
sys.path.insert(0, str(_PIPELINE_ROOT / "graph-data-api"))


def _load_module(module_name: str, file_path: pathlib.Path):
    spec = importlib.util.spec_from_file_location(module_name, file_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load module from {file_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class PipelineContractTests(unittest.TestCase):
    @unittest.skipUnless(importlib.util.find_spec("pydantic"), "pydantic dependency is required")
    def test_chunk_request_shape(self) -> None:
        chunk_models = _load_module("chunk_models", _PIPELINE_ROOT / "chunk-api" / "models.py")
        request = chunk_models.ChunkDocumentRequest(
            document_name="Doc",
            file_name="Doc.pdf",
            document_id="8f3f61d4-5803-4ccb-9de7-2114dd1cbad7",
            metadata={"pmc_id": "PMC0001", "doc_type": "biomedical_research_article"},
        )
        payload = request.model_dump(by_alias=True)
        self.assertEqual(payload["document_id"], "8f3f61d4-5803-4ccb-9de7-2114dd1cbad7")
        self.assertEqual(payload["metadata"]["pmc_id"], "PMC0001")

    @unittest.skipUnless(importlib.util.find_spec("pydantic"), "pydantic dependency is required")
    def test_embed_response_shape(self) -> None:
        embed_models = _load_module("embed_models", _PIPELINE_ROOT / "embed-api" / "models.py")
        response = embed_models.EmbedDocumentResponse(
            document_id="8f3f61d4-5803-4ccb-9de7-2114dd1cbad7",
            document_name="Doc2",
            total_chunks=2,
            message="ok",
        )
        payload = response.model_dump(by_alias=True)
        self.assertEqual(payload["document_name"], "Doc2")
        self.assertEqual(payload["total_chunks"], 2)

    @unittest.skipUnless(importlib.util.find_spec("pydantic"), "pydantic dependency is required")
    def test_enrich_graph_request_shape(self) -> None:
        graph_models = _load_module("graph_models", _PIPELINE_ROOT / "graph-data-api" / "models.py")
        request = graph_models.EnrichGraphRequest(
            document_id="doc-001",
            pmc_id="PMC12345",
            authors=["Smith J", "Jones A"],
            published_date="2023-06-01",
            publication_types=["Review"],
            topics=["Neoplasms", "Apoptosis"],
        )
        payload = request.model_dump()
        self.assertEqual(payload["document_id"], "doc-001")
        self.assertEqual(payload["pmc_id"], "PMC12345")
        self.assertEqual(payload["authors"], ["Smith J", "Jones A"])
        self.assertEqual(payload["published_date"], "2023-06-01")
        self.assertEqual(payload["publication_types"], ["Review"])
        self.assertEqual(payload["topics"], ["Neoplasms", "Apoptosis"])

    @unittest.skipUnless(importlib.util.find_spec("pydantic"), "pydantic dependency is required")
    def test_enrich_graph_request_defaults(self) -> None:
        graph_models = _load_module("graph_models", _PIPELINE_ROOT / "graph-data-api" / "models.py")
        request = graph_models.EnrichGraphRequest(document_id="doc-002")
        payload = request.model_dump()
        self.assertEqual(payload["pmc_id"], "")
        self.assertEqual(payload["authors"], [])
        self.assertEqual(payload["published_date"], "")
        self.assertEqual(payload["publication_types"], [])
        self.assertEqual(payload["topics"], [])

    @unittest.skipUnless(importlib.util.find_spec("pydantic"), "pydantic dependency is required")
    def test_local_query_ranker_returns_match(self) -> None:
        graph_models = _load_module("graph_models", _PIPELINE_ROOT / "graph-data-api" / "models.py")
        query_engine = _load_module("query_engine", _PIPELINE_ROOT / "graph-data-api" / "query_engine.py")
        common_models = _load_module("common_models", _PIPELINE_ROOT / "pipeline-common" / "pipeline_common" / "models.py")

        metadata = common_models.ChunkMetadata(
            document_id="PMC3",
            document_name="Doc3",
            source_system_doc_id="PMC3",
            doc_type="biomedical_research_article",
        )
        embedded = common_models.EmbeddedChunkData(
            chunk_id="PMC3:0",
            chunk_index=0,
            page_number=1,
            content="This chunk contains gene expression details.",
            metadata=metadata,
            embedding=[0.1, 0.2],
        )
        graph_document = common_models.GraphDocument(
            document_id="PMC3",
            doc_type="biomedical_research_article",
            total_chunks=1,
            nodes_created=2,
            chunks=[embedded],
            metadata={},
        )
        request = graph_models.QueryRequest(query="gene expression", top_k=5)
        ranked = query_engine.rank_chunks(request, [graph_document], max_results=10)
        self.assertEqual(len(ranked), 1)
        self.assertEqual(ranked[0].source_system_doc_id, "PMC3")
