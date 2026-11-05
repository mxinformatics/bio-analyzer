"""Command-line smoke tests for the Research API client."""

from __future__ import annotations

import argparse
import json
import sys
from typing import Any

from agentic_process.research import ResearchApiClient, ResearchApiError
from agentic_process.research.models import LiteratureIngestItem


def _print_json(data: Any) -> None:
    if hasattr(data, "model_dump"):
        payload = data.model_dump(by_alias=True, mode="json")
    else:
        payload = data
    print(json.dumps(payload, indent=2, default=str))


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="agentic-process",
        description="Smoke CLI for BioAnalyzer Research API (agentic-process learning workspace).",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    search = sub.add_parser("search", help="GET /Literature search")
    search.add_argument("query")
    search.add_argument("--start-index", type=int, default=0)

    summary = sub.add_parser("summary", help="GET /Literature/summary")
    summary.add_argument("ids", nargs="+", help="PubMed IDs")

    abstract = sub.add_parser("abstract", help="GET /Literature/abstract")
    abstract.add_argument("pmc_id")

    candidates = sub.add_parser("candidates", help="POST /Literature/candidates")
    candidates.add_argument("query")
    candidates.add_argument("--max", type=int, default=None, dest="max_candidates")

    graph = sub.add_parser("graph", help="POST /Chat/query (structured graph-RAG)")
    graph.add_argument("query")

    ingest = sub.add_parser("ingest", help="POST /Literature/ingest")
    ingest.add_argument("pmc_id")
    ingest.add_argument("--title", default="")
    ingest.add_argument("--doi", default="")
    ingest.add_argument("--pdf-link", default=None)
    ingest.add_argument("--xml-link", default=None)
    ingest.add_argument("--query-preview", default=None)

    status = sub.add_parser("ingest-status", help="GET /Literature/ingest/{jobId}")
    status.add_argument("job_id")

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)

    try:
        with ResearchApiClient.from_env() as client:
            if args.command == "search":
                _print_json(client.search_literature(args.query, start_index=args.start_index))
            elif args.command == "summary":
                _print_json(client.get_summaries(args.ids))
            elif args.command == "abstract":
                _print_json(client.get_abstract(args.pmc_id))
            elif args.command == "candidates":
                _print_json(
                    client.find_candidates(args.query, max_candidates=args.max_candidates)
                )
            elif args.command == "graph":
                _print_json(client.ask_graph(args.query))
            elif args.command == "ingest":
                item = LiteratureIngestItem(
                    pmcId=args.pmc_id,
                    title=args.title,
                    doi=args.doi,
                    downloadLink=args.pdf_link,
                    xmlLink=args.xml_link,
                )
                _print_json(
                    client.request_ingest(
                        [item],
                        query_preview=args.query_preview or args.title or args.pmc_id,
                    )
                )
            elif args.command == "ingest-status":
                _print_json(client.get_ingest_job_status(args.job_id))
            else:
                parser.error(f"Unknown command: {args.command}")
                return 2
    except ResearchApiError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    except ValueError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
