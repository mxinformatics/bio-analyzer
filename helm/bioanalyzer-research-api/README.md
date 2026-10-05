# BioAnalyzer Research API Helm Chart

This Helm chart deploys the BioAnalyzer Research API, a .NET 9.0 Web API for biological data analysis and research, to Kubernetes.

## Prerequisites

- Kubernetes 1.19+
- Helm 3.8.0+
- A container registry with the `dmaxim/bioanalyzer-research-api` image

## Installing the Chart

To install the chart with the release name `bioanalyzer-api`:

```bash
helm install bioanalyzer-api ./helm/bioanalyzer-research-api
```

To install with custom values:

```bash
helm install bioanalyzer-api ./helm/bioanalyzer-research-api -f my-values.yaml
```

## Uninstalling the Chart

To uninstall the `bioanalyzer-api` deployment:

```bash
helm uninstall bioanalyzer-api
```

## Configuration

The following table lists the configurable parameters of the BioAnalyzer Research API chart and their default values.

### Basic Configuration

| Parameter | Description | Default |
| --------- | ----------- | ------- |
| `replicaCount` | Number of replicas | `1` |
| `image.repository` | Image repository | `dmaxim/bioanalyzer-research-api` |
| `image.tag` | Image tag | `"latest"` |
| `image.pullPolicy` | Image pull policy | `IfNotPresent` |
| `nameOverride` | Override the name of the chart | `""` |
| `fullnameOverride` | Override the full name of the chart | `""` |

### Service Configuration

| Parameter | Description | Default |
| --------- | ----------- | ------- |
| `service.type` | Kubernetes service type | `ClusterIP` |
| `service.port` | Service port | `80` |
| `service.targetPort` | Container port | `8080` |

### Ingress Configuration

| Parameter | Description | Default |
| --------- | ----------- | ------- |
| `ingress.enabled` | Enable ingress | `false` |
| `ingress.className` | Ingress class name | `""` |
| `ingress.annotations` | Ingress annotations | `{}` |
| `ingress.hosts[0].host` | Hostname | `bioanalyzer-api.local` |
| `ingress.hosts[0].paths[0].path` | Path | `/` |
| `ingress.hosts[0].paths[0].pathType` | Path type | `Prefix` |
| `ingress.tls` | TLS configuration | `[]` |

### Resource Configuration

| Parameter | Description | Default |
| --------- | ----------- | ------- |
| `resources.requests.cpu` | CPU requests | `500m` |
| `resources.requests.memory` | Memory requests | `256Mi` |
| `resources.limits.cpu` | CPU limits | `1000m` |
| `resources.limits.memory` | Memory limits | `512Mi` |

### Autoscaling Configuration

| Parameter | Description | Default |
| --------- | ----------- | ------- |
| `autoscaling.enabled` | Enable HPA | `false` |
| `autoscaling.minReplicas` | Minimum replicas | `1` |
| `autoscaling.maxReplicas` | Maximum replicas | `10` |
| `autoscaling.targetCPUUtilizationPercentage` | Target CPU utilization | `80` |
| `autoscaling.targetMemoryUtilizationPercentage` | Target memory utilization | `80` |

### Azure Services Configuration

| Parameter | Description | Default |
| --------- | ----------- | ------- |
| `secrets.azure.create` | Create Azure secrets | `false` |
| `secrets.azure.existingSecret` | Use existing secret | `""` |
| `secrets.azure.storageConnectionString` | Azure Storage connection string | `""` |
| `secrets.azure.keyVaultUrl` | Azure Key Vault URL | `""` |
| `secrets.azure.searchServiceEndpoint` | Azure Search service endpoint | `""` |
| `secrets.azure.searchServiceApiKey` | Azure Search API key | `""` |
| `secrets.azure.openAIEndpoint` | Azure OpenAI endpoint | `""` |
| `secrets.azure.openAIApiKey` | Azure OpenAI API key | `""` |

## Examples

### Basic Installation

```bash
helm install bioanalyzer-api ./helm/bioanalyzer-research-api
```

### Installation with Ingress Enabled

```yaml
# values-ingress.yaml
ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
  hosts:
    - host: api.bioanalyzer.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: bioanalyzer-api-tls
      hosts:
        - api.bioanalyzer.example.com
```

```bash
helm install bioanalyzer-api ./helm/bioanalyzer-research-api -f values-ingress.yaml
```

### Installation with Azure Configuration

```yaml
# values-azure.yaml
secrets:
  azure:
    create: true
    storageConnectionString: "DefaultEndpointsProtocol=https;AccountName=..."
    keyVaultUrl: "https://your-keyvault.vault.azure.net/"
    searchServiceEndpoint: "https://your-search-service.search.windows.net"
    searchServiceApiKey: "your-api-key"
    openAIEndpoint: "https://your-openai.openai.azure.com/"
    openAIApiKey: "your-openai-key"

resources:
  requests:
    cpu: 1000m
    memory: 512Mi
  limits:
    cpu: 2000m
    memory: 1Gi

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 20
  targetCPUUtilizationPercentage: 70
```

```bash
helm install bioanalyzer-api ./helm/bioanalyzer-research-api -f values-azure.yaml
```

### Using External Secrets

Instead of creating secrets directly, you can use existing secrets:

```yaml
# values-external-secrets.yaml
secrets:
  azure:
    create: false
    existingSecret: "bioanalyzer-azure-secrets"
```

Create the secret externally:
```bash
kubectl create secret generic bioanalyzer-azure-secrets \
  --from-literal=AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=..." \
  --from-literal=AZURE_KEYVAULT_URL="https://..." \
  --from-literal=AZURE_SEARCH_SERVICE_ENDPOINT="https://..." \
  --from-literal=AZURE_SEARCH_SERVICE_API_KEY="..." \
  --from-literal=AZURE_OPENAI_ENDPOINT="https://..." \
  --from-literal=AZURE_OPENAI_API_KEY="..."
```

## Health Checks

The application includes health check endpoints:
- `/health` - Basic health check
- `/health/ready` - Readiness check

These are automatically configured in the deployment for liveness and readiness probes.

## Security

The chart includes several security best practices:
- Non-root user execution
- Security contexts with restricted capabilities
- Service account with minimal permissions
- Optional ingress with TLS support

## Monitoring

The application is configured with OpenTelemetry for observability. You can configure telemetry endpoints through environment variables or connect to observability platforms like Jaeger, Zipkin, or Azure Application Insights.

## Support

For issues and questions:
- GitHub Issues: https://github.com/mxinformatics/BioAnalyzer/issues
- Documentation: https://github.com/mxinformatics/BioAnalyzer


  