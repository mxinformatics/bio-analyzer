# BioAnalyzer App Helm Chart

This Helm chart deploys the BioAnalyzer.App (Blazor Server application) to Kubernetes.

## Prerequisites

- Kubernetes cluster (1.19+)
- Helm 3.0+
- Docker image `dmaxim/bioanalyzer-app` available in your registry

## Installation

### Quick Start

```bash
# Install with default values
helm install bioanalyzer-app ./helm/bioanalyzer-app

# Install in specific namespace
helm install bioanalyzer-app ./helm/bioanalyzer-app -n bioanalyzer --create-namespace

# Install with custom values
helm install bioanalyzer-app ./helm/bioanalyzer-app -f custom-values.yaml
```

### Configuration

#### Basic Configuration

The chart can be configured using the `values.yaml` file. Key configuration options include:

```yaml
# Application image
image:
  repository: dmaxim/bioanalyzer-app
  tag: "1.0.0"

# Service configuration
service:
  type: ClusterIP
  port: 80

# Resource limits
resources:
  limits:
    cpu: 500m
    memory: 512Mi
  requests:
    cpu: 250m
    memory: 256Mi
```

#### Azure Service Bus Integration

The application integrates with Azure Service Bus. You can configure authentication in two ways:

1. **Using Managed Identity (Recommended for Azure)**:
   ```yaml
   azureServiceBus:
     useManagedIdentity: true
   
   # Optionally specify the managed identity client ID
   env:
     - name: AZURE_CLIENT_ID
       value: "your-managed-identity-client-id"
   ```

2. **Using Connection String**:
   ```yaml
   azureServiceBus:
     useManagedIdentity: false
     connectionStringSecretName: "azure-servicebus-connection"
     connectionStringSecretKey: "connectionString"
   ```

   Create the secret:
   ```bash
   kubectl create secret generic azure-servicebus-connection \
     --from-literal=connectionString="Endpoint=sb://..." \
     -n bioanalyzer
   ```

#### Application Settings

Configure application settings through the `config.appSettings` section:

```yaml
config:
  aspnetcoreEnvironment: Production
  appSettings:
    Logging:
      LogLevel:
        Default: Information
        "Microsoft.AspNetCore": Warning
    AllowedHosts: "*"
    Events:
      ServiceBusNamespace: "your-servicebus.servicebus.windows.net"
      LiteratureDownloadTopic: "download-document-request"
```

#### Ingress Configuration

Enable ingress for external access:

```yaml
ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
  hosts:
    - host: bioanalyzer.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: bioanalyzer-tls
      hosts:
        - bioanalyzer.example.com
```

#### Autoscaling

Enable Horizontal Pod Autoscaler:

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

config:
  aspnetcoreEnvironment: Development

ingress:
  enabled: true
  hosts:
    - host: bioanalyzer-dev.local
      paths:
        - path: /
          pathType: Prefix

resources:
  requests:
    cpu: 100m
    memory: 128Mi
  limits:
    cpu: 200m
    memory: 256Mi
```

### Production Environment

```yaml
# values-prod.yaml
replicaCount: 3

config:
  aspnetcoreEnvironment: Production

image:
  tag: "v1.0.0"

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 20
  targetCPUUtilizationPercentage: 70

ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
  hosts:
    - host: bioanalyzer.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: bioanalyzer-tls
      hosts:
        - bioanalyzer.example.com

azureServiceBus:
  useManagedIdentity: true

resources:
  requests:
    cpu: 250m
    memory: 256Mi
  limits:
    cpu: 1000m
    memory: 1Gi
```

## Deployment Commands

```bash
# Development deployment
helm install bioanalyzer-app ./helm/bioanalyzer-app \
  -f values-dev.yaml \
  -n bioanalyzer-dev --create-namespace

# Production deployment
helm install bioanalyzer-app ./helm/bioanalyzer-app \
  -f values-prod.yaml \
  -n bioanalyzer-prod --create-namespace

# Upgrade deployment
helm upgrade bioanalyzer-app ./helm/bioanalyzer-app \
  -f values-prod.yaml \
  -n bioanalyzer-prod

# Rollback
helm rollback bioanalyzer-app 1 -n bioanalyzer-prod
```

## Monitoring and Troubleshooting

### Health Checks

The application includes built-in health checks:

- **Liveness Probe**: HTTP GET `/` on port 8080
- **Readiness Probe**: HTTP GET `/` on port 8080

### Logs

```bash
# View application logs
kubectl logs -f deployment/bioanalyzer-app -n bioanalyzer

# View all pod logs
kubectl logs -l app.kubernetes.io/name=bioanalyzer-app -n bioanalyzer --tail=100
```

### Status Checks

```bash
# Check deployment status
kubectl get deployments -n bioanalyzer

# Check pod status
kubectl get pods -l app.kubernetes.io/name=bioanalyzer-app -n bioanalyzer

# Check service status
kubectl get services -n bioanalyzer

# Check ingress status
kubectl get ingress -n bioanalyzer
```

### Common Issues

1. **ImagePullBackOff**: Ensure the Docker image is accessible and the tag is correct
2. **CrashLoopBackOff**: Check application logs for startup errors
3. **Service Bus Connection**: Verify Azure Service Bus configuration and credentials

## Security Considerations

- The application runs as a non-root user (UID: 1000)
- Read-only root filesystem for enhanced security
- Security context drops all capabilities
- ConfigMaps and Secrets are mounted read-only

## Contributing

To modify the chart:

1. Make changes to templates or values
2. Test with `helm template` or `helm install --dry-run`
3. Update the chart version in `Chart.yaml`
4. Test deployment in a development environment

## Chart Information

- **Chart Version**: 0.1.0
- **App Version**: 1.0.0
- **Kubernetes Version**: 1.19+
- **Helm Version**: 3.0+