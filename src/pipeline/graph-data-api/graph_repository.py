from __future__ import annotations

import json
import re
from typing import Any

from models import QueryRequest, ScoredDocumentResult
from pipeline_common import EmbeddedChunkData, GraphDocument


class Neo4jGraphRepository:
    def __init__(
        self,
        uri: str,
        username: str,
        password: str,
        database: str,
        max_query_results: int,
        query_embedder: Any | None = None,
        vector_index: str = "chunk_embedding_index",
        text_index: str = "chunk_text_index",
        vector_dimensions: int = 512,
        hybrid_alpha: float = 0.7,
    ) -> None:
        if not uri:
            raise ValueError("NEO4J_URI must be configured for neo4j graph backend.")
        if not username:
            raise ValueError("NEO4J_USERNAME must be configured for neo4j graph backend.")
        if not password:
            raise ValueError("NEO4J_PASSWORD must be configured for neo4j graph backend.")

        try:
            from neo4j import GraphDatabase
        except ImportError as ex:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "neo4j package is required for neo4j graph backend. "
                "Install dependencies from src/pipeline/requirements.txt."
            ) from ex

        self._driver = GraphDatabase.driver(uri, auth=(username, password))
        self._database = database
        self._max_query_results = max_query_results
        self._query_embedder = query_embedder
        self._vector_index = _sanitize_identifier(vector_index, "chunk_embedding_index")
        self._text_index = _sanitize_identifier(text_index, "chunk_text_index")
        self._vector_dimensions = max(64, vector_dimensions)
        self._hybrid_alpha = min(1.0, max(0.0, hybrid_alpha))
        self._ensure_schema()

    def build_graph(self, document_id: str, embedded_chunks: list[EmbeddedChunkData]) -> GraphDocument:
        first_chunk = embedded_chunks[0]
        doc_type = first_chunk.metadata.doc_type or "biomedical_research_article"
        document_name = first_chunk.metadata.document_name or document_id
        source_system_doc_id = first_chunk.metadata.source_system_doc_id or document_id
        document_metadata = _to_neo4j_properties(
            first_chunk.metadata.document_metadata or {},
            reserved_keys={
                "document_id",
                "document_name",
                "source_system_doc_id",
                "doc_type",
            },
        )

        with self._driver.session(database=self._database) as session:
            write_method = session.execute_write if hasattr(session, "execute_write") else session.write_transaction
            write_method(
                self._upsert_document_chunks,
                document_id,
                document_name,
                source_system_doc_id,
                doc_type,
                document_metadata,
                embedded_chunks,
            )

        nodes_created = 1 + len(embedded_chunks)
        return GraphDocument(
            document_id=document_id,
            doc_type=doc_type,
            total_chunks=len(embedded_chunks),
            nodes_created=nodes_created,
            chunks=embedded_chunks,
            metadata={"backend": "neo4j"},
        )

    def enrich_graph(
        self,
        document_id: str,
        pmc_id: str,
        authors: list[str],
        published_date: str,
        publication_types: list[str],
        topics: list[str],
        topic_source: str = "LLM",
    ) -> tuple[int, int]:
        with self._driver.session(database=self._database) as session:
            write_method = session.execute_write if hasattr(session, "execute_write") else session.write_transaction
            nodes_created, relationships_created = write_method(
                self._write_enrichment,
                document_id,
                pmc_id,
                authors,
                published_date,
                publication_types,
                topics,
                topic_source,
            )
        return nodes_created, relationships_created

    def query(self, request: QueryRequest) -> list[ScoredDocumentResult]:
        if not request.query.strip():
            return []

        query_embedding: list[float] | None = None
        if self._query_embedder is not None:
            try:
                query_embedding = self._query_embedder.embed_text(request.query)
            except Exception:  # noqa: BLE001
                query_embedding = None

        try:
            return self._execute_hybrid_query(
                request=request,
                query_embedding=query_embedding,
            )
        except Exception:  # noqa: BLE001
            return self._execute_lexical_query(request=request)

    def readiness_check(self) -> tuple[bool, str]:
        try:
            with self._driver.session(database=self._database) as session:
                result = session.run("RETURN 1 AS ok")
                row = result.single()
                if not row or row.get("ok") != 1:
                    return False, "Neo4j readiness query did not return expected value."
            return True, "Neo4j graph repository is ready."
        except Exception as ex:  # noqa: BLE001
            return False, f"Neo4j readiness check failed: {ex}"

    def close(self) -> None:
        self._driver.close()

    def _ensure_schema(self) -> None:
        schema_statements = [
            "CREATE CONSTRAINT document_id_unique IF NOT EXISTS FOR (d:Document) REQUIRE d.document_id IS UNIQUE",
            "CREATE CONSTRAINT chunk_key_unique IF NOT EXISTS FOR (c:Chunk) REQUIRE c.chunk_key IS UNIQUE",
            "CREATE INDEX chunk_doc_type IF NOT EXISTS FOR (c:Chunk) ON (c.doc_type)",
            (
                f"CREATE VECTOR INDEX {self._vector_index} IF NOT EXISTS FOR (c:Chunk) ON (c.embedding) "
                f"OPTIONS {{indexConfig: {{`vector.dimensions`: {self._vector_dimensions}, "
                "`vector.similarity_function`: 'cosine'}}}}"
            ),
            f"CREATE FULLTEXT INDEX {self._text_index} IF NOT EXISTS FOR (c:Chunk) ON EACH [c.text]",
            "CREATE INDEX author_name IF NOT EXISTS FOR (a:Author) ON (a.name)",
            "CREATE INDEX topic_name IF NOT EXISTS FOR (t:Topic) ON (t.name)",
        ]
        with self._driver.session(database=self._database) as session:
            for statement in schema_statements:
                try:
                    session.run(statement)
                except Exception:  # noqa: BLE001
                    continue

    @staticmethod
    def _upsert_document_chunks(
        tx: Any,
        document_id: str,
        document_name: str,
        source_system_doc_id: str,
        doc_type: str,
        document_metadata: dict[str, Any],
        embedded_chunks: list[EmbeddedChunkData],
    ) -> None:
        tx.run(
            """
            MERGE (d:Document {document_id: $document_id})
            SET d.document_name = $document_name,
                d.source_system_doc_id = $source_system_doc_id,
                d.doc_type = $doc_type
            WITH d
            SET d += $document_metadata
            """,
            {
                "document_id": document_id,
                "document_name": document_name,
                "source_system_doc_id": source_system_doc_id,
                "doc_type": doc_type,
                "document_metadata": document_metadata,
            },
        )

        for chunk in embedded_chunks:
            metadata_payload = _to_neo4j_metadata(chunk.metadata.model_dump(by_alias=True, exclude_none=True))
            metadata_json = json.dumps(metadata_payload, ensure_ascii=False)
            tx.run(
                """
                MATCH (d:Document {document_id: $document_id})
                MERGE (c:Chunk {chunk_key: $chunk_key})
                SET c.document_id = $document_id,
                    c.chunk_index = $chunk_index,
                    c.page_number = $page_number,
                    c.text = $text,
                    c.document_name = $document_name,
                    c.source_system_doc_id = $source_system_doc_id,
                    c.doc_type = $doc_type,
                    c.embedding = $embedding,
                    c.metadata = $metadata
                MERGE (d)-[:HAS_CHUNK]->(c)
                """,
                {
                    "chunk_key": f"{document_id}:{chunk.chunk_index}",
                    "document_id": document_id,
                    "chunk_index": chunk.chunk_index,
                    "page_number": chunk.page_number,
                    "text": chunk.content,
                    "document_name": chunk.metadata.document_name or document_name,
                    "source_system_doc_id": chunk.metadata.source_system_doc_id or source_system_doc_id,
                    "doc_type": chunk.metadata.doc_type or doc_type,
                    "embedding": chunk.embedding,
                    "metadata": metadata_json,
                },
            )

    @staticmethod
    def _write_enrichment(
        tx: Any,
        document_id: str,
        pmc_id: str,
        authors: list[str],
        published_date: str,
        publication_types: list[str],
        topics: list[str],
        topic_source: str,
    ) -> tuple[int, int]:
        nodes_created = 0
        relationships_created = 0

        tx.run(
            """
            MERGE (d:Document {document_id: $document_id})
            SET d.published_date = $published_date,
                d.pmc_id = $pmc_id,
                d.publication_types = $publication_types
            """,
            {
                "document_id": document_id,
                "published_date": published_date,
                "pmc_id": pmc_id,
                "publication_types": publication_types,
            },
        )

        if authors:
            result = tx.run(
                """
                MATCH (d:Document {document_id: $document_id})
                UNWIND $authors AS author_name
                MERGE (a:Author {name: author_name})
                MERGE (a)-[:AUTHORED]->(d)
                """,
                {"document_id": document_id, "authors": authors},
            )
            summary = result.consume()
            nodes_created += summary.counters.nodes_created
            relationships_created += summary.counters.relationships_created

        if topics:
            result = tx.run(
                """
                MATCH (d:Document {document_id: $document_id})
                UNWIND $topics AS topic_name
                MERGE (t:Topic {name: topic_name, source: $topic_source})
                MERGE (d)-[:COVERS]->(t)
                """,
                {"document_id": document_id, "topics": topics, "topic_source": topic_source or "LLM"},
            )
            summary = result.consume()
            nodes_created += summary.counters.nodes_created
            relationships_created += summary.counters.relationships_created

        return nodes_created, relationships_created

    def _execute_hybrid_query(
        self,
        request: QueryRequest,
        query_embedding: list[float] | None,
    ) -> list[ScoredDocumentResult]:
        top_k = min(request.top_k, self._max_query_results)
        document_types = _resolve_document_type_filters(request)

        cypher = """
        CALL {
            WITH $query_embedding AS query_embedding
            WITH query_embedding WHERE query_embedding IS NOT NULL
            CALL db.index.vector.queryNodes($vector_index, $top_k, query_embedding) YIELD node, score
            RETURN node, score AS vector_score, 0.0 AS text_score
            UNION
            CALL db.index.fulltext.queryNodes($text_index, $query_text, {limit: $top_k}) YIELD node, score
            RETURN node, 0.0 AS vector_score, score AS text_score
        }
        WITH node, max(vector_score) AS vector_score, max(text_score) AS text_score
        WHERE node IS NOT NULL
        WITH collect({node: node, vector_score: vector_score, text_score: text_score}) AS rows,
             max(vector_score) AS max_vector_score,
             max(text_score) AS max_text_score
        UNWIND rows AS row
        WITH row.node AS c,
             CASE
                 WHEN max_vector_score = 0 THEN 0.0
                 ELSE row.vector_score / max_vector_score
             END AS normalized_vector_score,
             CASE
                 WHEN max_text_score = 0 THEN 0.0
                 ELSE row.text_score / max_text_score
             END AS normalized_text_score
        WHERE size($document_types) = 0 OR coalesce(c.doc_type, '') IN $document_types
        WITH c, (($alpha * normalized_vector_score) + ((1.0 - $alpha) * normalized_text_score)) AS score
        RETURN score,
               c.text AS text,
               toInteger(c.page_number) AS page_number,
               toInteger(c.chunk_index) AS chunk_index,
               coalesce(c.document_name, '') AS document_name,
               coalesce(c.source_system_doc_id, '') AS source_system_doc_id,
               c.doc_type AS document_type,
               c.metadata AS metadata
        ORDER BY score DESC, chunk_index ASC
        LIMIT $top_k
        """

        with self._driver.session(database=self._database) as session:
            result = session.run(
                cypher,
                {
                    "query_embedding": query_embedding,
                    "vector_index": self._vector_index,
                    "text_index": self._text_index,
                    "query_text": request.query,
                    "document_types": document_types,
                    "top_k": top_k,
                    "alpha": self._hybrid_alpha,
                },
            )
            rows = list(result)
        return _map_rows_to_scored_results(rows)

    def _execute_lexical_query(self, request: QueryRequest) -> list[ScoredDocumentResult]:
        query_tokens = _tokenize(request.query)
        if not query_tokens:
            return []

        top_k = min(request.top_k, self._max_query_results)
        document_types = _resolve_document_type_filters(request)

        cypher = """
        MATCH (c:Chunk)
        WHERE size($document_types) = 0 OR coalesce(c.doc_type, '') IN $document_types
        WITH c, reduce(score = 0.0, token IN $query_tokens |
            score + CASE WHEN toLower(c.text) CONTAINS token THEN 1.0 ELSE 0.0 END
        ) AS token_score
        WHERE token_score > 0.0
        RETURN token_score / toFloat(size($query_tokens)) AS score,
               c.text AS text,
               toInteger(c.page_number) AS page_number,
               toInteger(c.chunk_index) AS chunk_index,
               coalesce(c.document_name, '') AS document_name,
               coalesce(c.source_system_doc_id, '') AS source_system_doc_id,
               c.doc_type AS document_type,
               c.metadata AS metadata
        ORDER BY score DESC, chunk_index ASC
        LIMIT $top_k
        """

        with self._driver.session(database=self._database) as session:
            result = session.run(
                cypher,
                {
                    "query_tokens": query_tokens,
                    "document_types": document_types,
                    "top_k": top_k,
                },
            )
            rows = list(result)
        return _map_rows_to_scored_results(rows)


def _resolve_document_type_filters(request: QueryRequest) -> list[str]:
    filters: list[str] = []
    if request.document_type:
        filters.append(request.document_type)
    if request.document_types:
        filters.extend(request.document_types)
    return list(dict.fromkeys([value for value in filters if value]))


def _tokenize(value: str) -> list[str]:
    return re.findall(r"[a-zA-Z0-9_]+", (value or "").lower())


def _to_neo4j_metadata(payload: dict[str, Any]) -> dict[str, Any]:
    normalized: dict[str, Any] = {}
    for key, value in payload.items():
        normalized_value = _to_neo4j_property_value(value)
        if normalized_value is not None:
            normalized[key] = normalized_value
    return normalized


def _to_neo4j_properties(payload: dict[str, Any], reserved_keys: set[str] | None = None) -> dict[str, Any]:
    reserved = reserved_keys or set()
    normalized: dict[str, Any] = {}
    for raw_key, value in payload.items():
        key = _sanitize_property_key(raw_key)
        if not key or key in reserved:
            continue
        normalized_value = _to_neo4j_property_value(value)
        if normalized_value is None:
            continue
        normalized[key] = normalized_value
    return normalized


def _to_neo4j_property_value(value: Any) -> Any:
    if value is None:
        return None
    if isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, (list, tuple)):
        normalized_items: list[Any] = []
        for item in value:
            normalized_item = _to_neo4j_property_value(item)
            if isinstance(normalized_item, (str, int, float, bool)):
                normalized_items.append(normalized_item)
        return normalized_items
    if isinstance(value, dict):
        return json.dumps(value, ensure_ascii=False)
    return str(value)


def _sanitize_property_key(value: Any) -> str:
    if not isinstance(value, str):
        return ""
    cleaned = re.sub(r"[^A-Za-z0-9_]", "_", value.strip())
    cleaned = re.sub(r"_+", "_", cleaned).strip("_")
    if not cleaned:
        return ""
    if cleaned[0].isdigit():
        cleaned = f"m_{cleaned}"
    return cleaned


def _map_rows_to_scored_results(rows: list[Any]) -> list[ScoredDocumentResult]:
    scored_results: list[ScoredDocumentResult] = []
    for row in rows:
        metadata_payload = row.get("metadata")
        if isinstance(metadata_payload, str):
            try:
                metadata_payload = json.loads(metadata_payload)
            except Exception:  # noqa: BLE001
                metadata_payload = None

        scored_results.append(
            ScoredDocumentResult(
                score=float(row.get("score") or 0.0),
                text=row.get("text") or "",
                page_number=int(row.get("page_number") or 0),
                chunk_index=int(row.get("chunk_index") or 0),
                document_name=row.get("document_name") or "",
                source_system_doc_id=row.get("source_system_doc_id") or "",
                document_type=row.get("document_type"),
                metadata=metadata_payload if isinstance(metadata_payload, dict) else None,
            )
        )
    return scored_results


def _sanitize_identifier(value: str, fallback: str) -> str:
    if not value:
        return fallback
    if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", value):
        return value
    return fallback
