FROM python:3.12-slim

WORKDIR /app

COPY requirements.base.txt /app/requirements.base.txt
COPY graph-data-api/requirements.txt /app/graph-data-api/requirements.txt
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r /app/graph-data-api/requirements.txt

COPY pipeline-common /app/pipeline-common
COPY graph-data-api /app/graph-data-api

WORKDIR /app/graph-data-api
ENV PYTHONPATH=/app/pipeline-common

EXPOSE 8080
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"]
