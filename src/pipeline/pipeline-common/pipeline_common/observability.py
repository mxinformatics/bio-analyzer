from __future__ import annotations

import time
import uuid
from typing import Any

from fastapi import FastAPI, Request

CORRELATION_ID_HEADER = "X-Correlation-Id"


def install_request_observability(app: FastAPI, logger: Any, service_name: str) -> None:
    @app.middleware("http")
    async def request_observability_middleware(request: Request, call_next):
        correlation_id = request.headers.get(CORRELATION_ID_HEADER) or uuid.uuid4().hex
        request.state.correlation_id = correlation_id
        request_started_at = time.perf_counter()

        try:
            response = await call_next(request)
        except Exception as ex:  # noqa: BLE001
            duration_ms = (time.perf_counter() - request_started_at) * 1000.0
            logger.exception(
                "request.failed service=%s method=%s path=%s duration_ms=%.2f correlation_id=%s error=%s",
                service_name,
                request.method,
                request.url.path,
                duration_ms,
                correlation_id,
                ex,
            )
            raise

        duration_ms = (time.perf_counter() - request_started_at) * 1000.0
        response.headers[CORRELATION_ID_HEADER] = correlation_id
        logger.info(
            "request.completed service=%s method=%s path=%s status_code=%s duration_ms=%.2f correlation_id=%s",
            service_name,
            request.method,
            request.url.path,
            response.status_code,
            duration_ms,
            correlation_id,
        )
        return response


def get_correlation_id(request: Request | None) -> str:
    if request is None:
        return ""
    return str(getattr(request.state, "correlation_id", ""))


def log_stage_metrics(logger: Any, stage: str, correlation_id: str, **metrics: Any) -> None:
    metric_pairs = " ".join(f"{key}={value}" for key, value in metrics.items())
    logger.info(
        "stage.metrics stage=%s correlation_id=%s %s",
        stage,
        correlation_id or "n/a",
        metric_pairs,
    )
