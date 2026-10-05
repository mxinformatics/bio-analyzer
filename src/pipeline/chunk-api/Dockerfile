FROM python:3.12-slim

WORKDIR /app

COPY requirements.base.txt /app/requirements.base.txt
COPY chunk-api/requirements.txt /app/chunk-api/requirements.txt
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r /app/chunk-api/requirements.txt

COPY pipeline-common /app/pipeline-common
COPY chunk-api /app/chunk-api

WORKDIR /app/chunk-api
ENV PYTHONPATH=/app/pipeline-common

EXPOSE 8001
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8001"]
