# BioAnalyzer EventHandlers Helm Chart

This Helm chart deploys the BioAnalyzer EventHandlers (Azure Functions application) to Kubernetes. The EventHandlers process Azure Service Bus messages for document downloading and text processing.

## Overview

The BioAnalyzer EventHandlers is a .NET 9.0 Azure Functions application that:

- Listens to Azure Service Bus queues for download requests
- Downloads scientific literature from various sources
- Extracts PDFs from compressed archives (tar.gz)
- Stores downloaded files in Azure Storage
- Publishes events when downloads are complete

## Prerequisites

- Kubernetes cluster (1.19+)
- Helm 3.0+
- Docker image `dmaxim/bioanalyzer-event-handlers` available in your registry
- Azure Service Bus namespace with appropriate queues and topics
- Azure Storage Account for storing downloaded files
- Application Insights (optional, for monitoring)

## Installation

### Quick Start

```bash
# Install with default values
helm install bioanalyzer-event-handlers ./helm/bioanalyzer-event-handlers

# Install in specific namespace
helm install bioanalyzer-event-handlers ./helm/bioanalyzer-event-handlers -n bioanalyzer --create-namespace

# Install with custom values
helm install bioanalyzer-event-handlers ./helm/bioanalyzer-event-handlers -f custom-values.yaml
```

### Configuration

#### Basic Configuration

```yaml
# Application image
image:
  repository: dmaxim/bioanalyzer-event-handlers
  tag: "1.0.0"

# Resource allocation
resources:
  limits:
    cpu: 1000m
    memory: 1Gi
  requests:
    cpu: 500m
    memory: 512Mi
```

#### Azure Storage Configuration

The application requires Azure Storage for storing downloaded literature files.

**Option 1: Using Managed Identity (Recommended for Azure)**
```yaml
azureStorage:
  useManagedIdentity: true
  clientId: "your-storage-managed-identity-client-id"
  downloadFileContainer: "downloaded-literature"

env:
  - name: AZURE_CLIENT_ID
    value: "your-storage-managed-identity-client-id"
```

**Option 2: Using Connection String**
```yaml
azureStorage:
  useManagedIdentity: false
  connectionStringSecretName: "azure-storage-connection"
  connectionStringSecretKey: "connectionString"
  downloadFileContainer: "downloaded-literature"
```

Create the storage secret:
```bash
kubectl create secret generic azure-storage-connection \
  --from-literal=connectionString="DefaultEndpointsProtocol=https;AccountName=..." \
  -n bioanalyzer
```

#### Azure Service Bus Configuration

The application processes messages from Azure Service Bus queues and publishes to topics.

**Option 1: Using Managed Identity (Recommended for Azure)**
```yaml
azureServiceBus:
  useManagedIdentity: true
  clientId: "your-servicebus-managed-identity-client-id"
  queues:
    downloadDocumentQueue: "download-document-request"
  topics:
    documentDownloadedTopic: "document-downloaded"

env:
  - name: AZURE_CLIENT_ID
    value: "your-servicebus-managed-identity-client-id"
```

**Option 2: Using Connection Strings**
```yaml
azureServiceBus:
  useManagedIdentity: false
  listenConnectionStringSecretName: "azure-servicebus-listen"
  listenConnectionStringSecretKey: "connectionString"
  sendConnectionStringSecretName: "azure-servicebus-send" 
  sendConnectionStringSecretKey: "connectionString"
  queues:
    downloadDocumentQueue: "download-document-request"
  topics:
    documentDownloadedTopic: "document-downloaded"
```

Create the Service Bus secrets:
```bash
# For listening to queues
kubectl create secret generic azure-servicebus-listen \
  --from-literal=connectionString="Endpoint=sb://...;SharedAccessKey=..." \
  -n bioanalyzer

# For sending to topics
kubectl create secret generic azure-servicebus-send \
  --from-literal=connectionString="Endpoint=sb://...;SharedAccessKey=..." \
  -n bioanalyzer
```

#### Application Insights Configuration

Enable monitoring and telemetry with Application Insights:

```yaml
applicationInsights:
  enabled: true
  connectionStringSecretName: "application-insights-connection"
  connectionStringSecretKey: "connectionString"
```

Create the Application Insights secret:
```bash
kubectl create secret generic application-insights-connection \
  --from-literal=connectionString="InstrumentationKey=...;IngestionEndpoint=..." \
  -n bioanalyzer
```

#### Azure Functions Configuration

Configure Azure Functions runtime settings:

```yaml
azureFunctions:
  runtime:
    version: "4"
    workerRuntime: "dotnet-isolated"
  host:
    detailedErrors: true
    functionTimeout: 5  # minutes
```

#### Autoscaling Configuration

Enable horizontal pod autoscaling based on CPU and memory:

```yaml
autoscaling:
  enabled: true
  minReplicas: 1
  maxReplicas: 10
  targetCPUUtilizationPercentage: 80
  targetMemoryUtilizationPercentage: 80
```

## Environment-Specific Examples

### Development Environment

```yaml
# values-dev.yaml
replicaCount: 1

image:
  tag: "latest"
  pullPolicy: Always

# Enable service for monitoring
service:
  enabled: true
  type: ClusterIP
  port: 80

# Use connection strings for development
azureStorage:
  useManagedIdentity: false
  connectionStringSecretName: "azure-storage-dev-connection"
  downloadFileContainer: "downloaded-literature-dev"

azureServiceBus:
  useManagedIdentity: false
  listenConnectionStringSecretName: "azure-servicebus-dev-connection"
  sendConnectionStringSecretName: "azure-servicebus-dev-connection"
  queues:
    downloadDocumentQueue: "download-document-request-dev"
  topics:
    documentDownloadedTopic: "document-downloaded-dev"

# Development logging
monitoring:
  logging:
    level: "Debug"

resources:
  requests:
    cpu: 200m
    memory: 256Mi
  limits:
    cpu: 500m
    memory: 512Mi

autoscaling:
  enabled: false
```

### Production Environment

```yaml
# values-prod.yaml
replicaCount: 2

image:
  tag: "v1.0.0"
  pullPolicy: IfNotPresent

# Disable external service in production
service:
  enabled: false

# Use managed identity for production
azureStorage:
  useManagedIdentity: true
  clientId: "your-storage-managed-identity-client-id"
  downloadFileContainer: "downloaded-literature"

azureServiceBus:
  useManagedIdentity: true
  clientId: "your-servicebus-managed-identity-client-id"

# Enable autoscaling
autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 20
  targetCPUUtilizationPercentage: 70

# Production resources
resources:
  requests:
    cpu: 500m
    memory: 512Mi
  limits:
    cpu: 1500m
    memory: 2Gi

# Anti-affinity for high availability
affinity:
  podAntiAffinity:
    preferredDuringSchedulingIgnoredDuringExecution:
    - weight: 100
      podAffinityTerm:
        labelSelector:
          matchExpressions:
          - key: app.kubernetes.io/name
            operator: In
            values:
            - bioanalyzer-event-handlers
        topologyKey: kubernetes.io/hostname
```

## Deployment Commands

```bash
# Development deployment
helm install bioanalyzer-event-handlers ./helm/bioanalyzer-event-handlers \
  -f values-dev.yaml \
  -n bioanalyzer-dev --create-namespace

# Production deployment
helm install bioanalyzer-event-handlers ./helm/bioanalyzer-event-handlers \
  -f values-prod.yaml \
  -n bioanalyzer-prod --create-namespace

# Upgrade deployment
helm upgrade bioanalyzer-event-handlers ./helm/bioanalyzer-event-handlers \
  -f values-prod.yaml \
  -n bioanalyzer-prod

# Check deployment status
helm status bioanalyzer-event-handlers -n bioanalyzer-prod
```

## Monitoring and Troubleshooting

### Health Checks

The application includes health checks:
- **Liveness Probe**: HTTP GET `/` on port 80
- **Readiness Probe**: HTTP GET `/` on port 80

### Application Insights

When enabled, Application Insights provides:
- Function execution metrics
- Error tracking and diagnostics  
- Performance monitoring
- Custom telemetry

### Logs and Monitoring

```bash
# View application logs
kubectl logs -f deployment/bioanalyzer-event-handlers -n bioanalyzer

# Check pod status
kubectl get pods -l app.kubernetes.io/name=bioanalyzer-event-handlers -n bioanalyzer

# Monitor resource usage
kubectl top pods -l app.kubernetes.io/name=bioanalyzer-event-handlers -n bioanalyzer

# Check HPA status (if enabled)
kubectl get hpa bioanalyzer-event-handlers -n bioanalyzer
```

### Function-Specific Monitoring

```bash
# Check if functions are processing messages
kubectl logs -f deployment/bioanalyzer-event-handlers -n bioanalyzer --since=5m | grep "Message ID"

# Monitor download operations
kubectl logs -f deployment/bioanalyzer-event-handlers -n bioanalyzer --since=5m | grep -E "(downloading|uploaded)"
```

### Common Issues and Solutions

#### Pod Won't Start

1. **ImagePullBackOff**
   ```bash
   # Check if image exists and is accessible
   kubectl describe pod <pod-name> -n bioanalyzer
   ```

2. **Configuration Issues**
   ```bash
   # Verify secrets exist
   kubectl get secrets -n bioanalyzer
   
   # Check ConfigMap
   kubectl get configmap bioanalyzer-event-handlers-config -n bioanalyzer -o yaml
   ```

#### No Messages Being Processed

1. **Service Bus Connection**
   - Verify Service Bus connection strings or managed identity
   - Check queue and topic names
   - Ensure proper permissions (Listen, Send)

2. **Azure Storage Access**
   - Verify storage connection string or managed identity
   - Check container exists and has proper permissions
   - Monitor Application Insights for storage-related errors

#### Performance Issues

1. **Resource Constraints**
   ```bash
   # Check resource usage
   kubectl top pods -n bioanalyzer
   
   # Review resource requests/limits
   kubectl describe deployment bioanalyzer-event-handlers -n bioanalyzer
   ```

2. **Scaling Issues**
   ```bash
   # Check HPA status
   kubectl get hpa -n bioanalyzer
   kubectl describe hpa bioanalyzer-event-handlers -n bioanalyzer
   ```

## Security Considerations

- Uses non-root user (UID: 1000)
- Drops all capabilities except required ones
- Supports Azure Managed Identity for secure authentication
- ConfigMaps and Secrets mounted read-only where possible
- Security contexts configured for enhanced security

## Function Details

### DownloadRequestHandler

Processes messages from the download queue:
- Downloads files from provided URLs
- Handles both direct PDF downloads and compressed archives
- Extracts PDFs from tar.gz files
- Uploads processed files to Azure Storage
- Publishes completion events

### BuildDocumentListHandler

Processes document list building requests (if applicable).

## Required Azure Resources

1. **Service Bus Namespace** with:
   - Queue: `download-document-request`
   - Topic: `document-downloaded`

2. **Storage Account** with:
   - Container: `downloaded-literature`

3. **Application Insights** (optional but recommended)

4. **Managed Identity** (recommended for production) with permissions for:
   - Service Bus Data Receiver (for queues)
   - Service Bus Data Sender (for topics)
   - Storage Blob Data Contributor (for containers)

## Chart Information

- **Chart Version**: 0.1.0
- **App Version**: 1.0.0
- **Kubernetes Version**: 1.19+
- **Helm Version**: 3.0+
- **Azure Functions Version**: 4.0
- **.NET Version**: 9.0