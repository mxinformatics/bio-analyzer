"""Errors raised by the Research API client."""

from __future__ import annotations


class ResearchApiError(Exception):
    """Base error for Research API client failures."""

    def __init__(self, message: str, *, status_code: int | None = None, body: str | None = None) -> None:
        super().__init__(message)
        self.status_code = status_code
        self.body = body


class ResearchApiConfigError(ResearchApiError):
    """Raised when client configuration is missing or invalid."""


class ResearchApiHttpError(ResearchApiError):
    """Raised when the Research API returns a non-success HTTP status."""
