"""Environment-backed configuration for agentic-process."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv

from agentic_process.research.exceptions import ResearchApiConfigError

_DEFAULT_API_KEY_HEADER = "X-Api-Key"
_DEFAULT_TIMEOUT_SECONDS = 60.0
_DEFAULT_BASE_URL = "http://localhost:5179"


def _load_env_files() -> None:
    """Load .env from the project root (and cwd) if present."""
    # Package lives at src/agentic_process/... → project root is parents[2]
    project_root = Path(__file__).resolve().parents[2]
    load_dotenv(project_root / ".env", override=False)
    load_dotenv(override=False)


def _first_env(*names: str) -> str:
    for name in names:
        value = os.getenv(name)
        if value is not None and value.strip():
            return value.strip()
    return ""


@dataclass(frozen=True, slots=True)
class ResearchApiSettings:
    """Settings required to call BioAnalyzer.Research.Api."""

    base_url: str
    api_key: str = ""
    bearer_token: str = ""
    api_key_header: str = _DEFAULT_API_KEY_HEADER
    timeout_seconds: float = _DEFAULT_TIMEOUT_SECONDS

    @property
    def has_credentials(self) -> bool:
        return bool(self.bearer_token or self.api_key)


def load_research_api_settings(*, require_base_url: bool = True) -> ResearchApiSettings:
    """
    Load Research API settings from environment variables.

    Supported variables:
      - RESEARCH_API_BASE_URL (or Aspire-style services__researchApi__http__0)
      - RESEARCH_API_KEY (or ApiAuthentication__ApiKey)
      - RESEARCH_API_BEARER_TOKEN (optional; preferred over API key when set)
      - RESEARCH_API_KEY_HEADER (default X-Api-Key)
      - RESEARCH_API_TIMEOUT_SECONDS (default 60)
    """
    _load_env_files()

    base_url = _first_env(
        "RESEARCH_API_BASE_URL",
        "services__researchApi__http__0",
        "services__researchApi__https__0",
    ).rstrip("/")

    if not base_url:
        if require_base_url:
            raise ResearchApiConfigError(
                "Research API base URL not configured. "
                "Set RESEARCH_API_BASE_URL (e.g. http://localhost:5179)."
            )
        base_url = _DEFAULT_BASE_URL

    timeout_raw = _first_env("RESEARCH_API_TIMEOUT_SECONDS")
    timeout_seconds = float(timeout_raw) if timeout_raw else _DEFAULT_TIMEOUT_SECONDS

    api_key_header = _first_env("RESEARCH_API_KEY_HEADER") or _DEFAULT_API_KEY_HEADER

    return ResearchApiSettings(
        base_url=base_url,
        api_key=_first_env("RESEARCH_API_KEY", "ApiAuthentication__ApiKey"),
        bearer_token=_first_env("RESEARCH_API_BEARER_TOKEN"),
        api_key_header=api_key_header,
        timeout_seconds=timeout_seconds,
    )
