FROM python:3.12-slim

WORKDIR /app

COPY requirements.base.txt /app/requirements.base.txt
COPY embed-api/requirements.txt /app/embed-api/requirements.txt
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r /app/embed-api/requirements.txt

COPY pipeline-common /app/pipeline-common
COPY embed-api /app/embed-api

WORKDIR /app/embed-api
ENV PYTHONPATH=/app/pipeline-common

EXPOSE 8002
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8002"]
