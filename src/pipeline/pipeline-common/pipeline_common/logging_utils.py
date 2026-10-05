import logging
from typing import Optional


class _ServiceContextFilter(logging.Filter):
    def __init__(self, service_name: str) -> None:
        super().__init__()
        self._service_name = service_name

    def filter(self, record: logging.LogRecord) -> bool:
        record.service_name = self._service_name
        return True


def configure_logging(service_name: str, logger_name: Optional[str] = None) -> logging.Logger:
    logger = logging.getLogger(logger_name or service_name)
    if logger.handlers:
        return logger

    logger.setLevel(logging.INFO)
    handler = logging.StreamHandler()
    handler.setFormatter(
        logging.Formatter(
            fmt="%(asctime)s %(levelname)s service=%(service_name)s logger=%(name)s %(message)s",
            datefmt="%Y-%m-%dT%H:%M:%S%z",
        )
    )
    handler.addFilter(_ServiceContextFilter(service_name))
    logger.addHandler(handler)
    logger.propagate = False
    return logger
