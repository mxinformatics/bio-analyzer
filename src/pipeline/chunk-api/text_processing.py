from __future__ import annotations

import re
import xml.etree.ElementTree as element_tree


def parse_source_content(content: bytes, source_type: str) -> list[tuple[int, str]]:
    normalized_source_type = (source_type or "").strip().lower()
    if normalized_source_type in {"xml", "jats_xml", "pmc_xml", "nxml"}:
        return _parse_xml_content(content)
    return _parse_pdf_or_text_content(content)


def _parse_xml_content(content: bytes) -> list[tuple[int, str]]:
    try:
        root = element_tree.fromstring(content.decode("utf-8", errors="ignore"))
    except element_tree.ParseError:
        return [(1, _normalize_whitespace(content.decode("utf-8", errors="ignore")))]

    extracted_sections: list[str] = []
    for node in root.iter():
        if node.text:
            text_value = _normalize_whitespace(node.text)
            if len(text_value) >= 30:
                extracted_sections.append(text_value)

    merged = "\n\n".join(extracted_sections)
    if not merged.strip():
        merged = _normalize_whitespace(content.decode("utf-8", errors="ignore"))
    return [(1, merged)]


def _parse_pdf_or_text_content(content: bytes) -> list[tuple[int, str]]:
    try:
        from pypdf import PdfReader  # type: ignore

        from io import BytesIO

        reader = PdfReader(BytesIO(content))
        pages: list[tuple[int, str]] = []
        for index, page in enumerate(reader.pages, start=1):
            extracted = _normalize_whitespace(page.extract_text() or "")
            if extracted:
                pages.append((index, extracted))
        if pages:
            return pages
    except Exception:  # noqa: BLE001
        pass

    fallback_text = _normalize_whitespace(content.decode("utf-8", errors="ignore"))
    return [(1, fallback_text)]


def chunk_pages(pages: list[tuple[int, str]], chunk_size: int, chunk_overlap: int) -> list[tuple[int, str]]:
    chunk_size = max(200, chunk_size)
    chunk_overlap = max(0, min(chunk_overlap, chunk_size // 2))
    step_size = max(1, chunk_size - chunk_overlap)

    chunked: list[tuple[int, str]] = []
    for page_number, text in pages:
        cleaned = _normalize_whitespace(text)
        if not cleaned:
            continue
        if len(cleaned) <= chunk_size:
            chunked.append((page_number, cleaned))
            continue

        start_index = 0
        while start_index < len(cleaned):
            window = cleaned[start_index : start_index + chunk_size]
            if window:
                chunked.append((page_number, window))
            start_index += step_size

    return chunked


def _normalize_whitespace(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()
