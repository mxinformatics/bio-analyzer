#!/bin/bash
set -e

# BioAnalyzer EventHandlers Deployment Script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHART_PATH="$SCRIPT_DIR"

# Default values
ENVIRONMENT="dev"
NAMESPACE=""
RELEASE_NAME="bioanalyzer-event-handlers"
DRY_RUN=""
UPGRADE=""

usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Deploy BioAnalyzer EventHandlers using Helm

Options:
    -e, --environment ENV    Environment (dev|prod) [default: dev]
    -n, --namespace NS       Kubernetes namespace [default: bioanalyzer-eh-ENV]
    -r, --release NAME       Helm release name [default: bioanalyzer-event-handlers]
    -u, --upgrade           Use helm upgrade instead of install
    --dry-run               Perform a dry run (template only)
    -h, --help              Show this help message

Examples:
    # Install in development
    $0 -e dev

    # Install in production
    $0 -e prod -n bioanalyzer-production

    # Upgrade existing deployment
    $0 -e prod -u

    # Dry run for production
    $0 -e prod --dry-run

Prerequisites:
    - kubectl configured and connected to cluster
    - Helm 3.x installed
    - Docker image dmaxim/bioanalyzer-event-handlers available
    - Azure Service Bus namespace with queues and topics
    - Azure Storage Account with container
    - Required secrets created (if not using managed identity)

Required Azure Resources:
    - Service Bus Queue: download-document-request
    - Service Bus Topic: document-downloaded
    - Storage Container: downloaded-literature
    - Application Insights (optional)

EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        -r|--release)
            RELEASE_NAME="$2"
            shift 2
            ;;
        -u|--upgrade)
            UPGRADE="true"
            shift
            ;;
        --dry-run)
            DRY_RUN="--dry-run"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

# Set default namespace if not provided
if [ -z "$NAMESPACE" ]; then
    NAMESPACE="bioanalyzer-eh-$ENVIRONMENT"
fi

# Validate environment
if [[ "$ENVIRONMENT" != "dev" && "$ENVIRONMENT" != "prod" ]]; then
    echo "Error: Environment must be 'dev' or 'prod'"
    exit 1
fi

# Set values file based on environment
VALUES_FILE="$CHART_PATH/values-$ENVIRONMENT.yaml"
if [ ! -f "$VALUES_FILE" ]; then
    echo "Error: Values file not found: $VALUES_FILE"
    exit 1
fi

echo "=== BioAnalyzer EventHandlers Deployment ==="
echo "Environment: $ENVIRONMENT"
echo "Namespace: $NAMESPACE"
echo "Release: $RELEASE_NAME"
echo "Values file: $VALUES_FILE"
echo "Chart path: $CHART_PATH"

if [ -n "$DRY_RUN" ]; then
    echo "Mode: Dry run"
elif [ -n "$UPGRADE" ]; then
    echo "Mode: Upgrade"
else
    echo "Mode: Install"
fi

echo "============================================"

# Check if helm is available
if ! command -v helm &> /dev/null; then
    echo "Error: Helm is not installed or not in PATH"
    exit 1
fi

# Check if kubectl is available and can connect
if ! kubectl cluster-info &> /dev/null; then
    echo "Error: kubectl is not configured or cannot connect to cluster"
    exit 1
fi

# Create namespace if it doesn't exist (except for dry-run)
if [ -z "$DRY_RUN" ]; then
    if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
        echo "Creating namespace: $NAMESPACE"
        kubectl create namespace "$NAMESPACE"
    fi
fi

# Function to check if secret exists
check_secret() {
    local secret_name="$1"
    local namespace="$2"
    kubectl get secret "$secret_name" -n "$namespace" &> /dev/null
}

# For production, check if secrets or managed identity configuration is ready
if [[ "$ENVIRONMENT" == "prod" && -z "$DRY_RUN" ]]; then
    echo "Checking production prerequisites..."
    
    MISSING_SECRETS=()
    
    # Check Application Insights secret
    if ! check_secret "application-insights-prod-connection" "$NAMESPACE"; then
        MISSING_SECRETS+=("application-insights-prod-connection")
    fi
    
    # For managed identity, we can't easily verify setup, so just warn
    echo "⚠️  Production uses managed identity for Azure resources"
    echo "   Ensure the following are configured:"
    echo "   - Workload identity is set up in the cluster"
    echo "   - Managed identities have proper permissions:"
    echo "     • Storage Blob Data Contributor (for storage)"
    echo "     • Service Bus Data Receiver (for queues)"
    echo "     • Service Bus Data Sender (for topics)"
    
    if [ ${#MISSING_SECRETS[@]} -gt 0 ]; then
        echo ""
        echo "⚠️  Missing secrets in namespace $NAMESPACE:"
        for secret in "${MISSING_SECRETS[@]}"; do
            echo "   - $secret"
        done
        echo ""
        echo "Create missing secrets before deployment:"
        for secret in "${MISSING_SECRETS[@]}"; do
            echo "kubectl create secret generic $secret --from-literal=connectionString=\"...\" -n $NAMESPACE"
        done
        echo ""
        read -p "Continue anyway? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
fi

# For development, check connection string secrets
if [[ "$ENVIRONMENT" == "dev" && -z "$DRY_RUN" ]]; then
    echo "Checking development prerequisites..."
    
    MISSING_SECRETS=()
    
    # Check required development secrets
    DEV_SECRETS=(
        "azure-storage-dev-connection"
        "azure-servicebus-dev-connection" 
        "application-insights-dev-connection"
    )
    
    for secret in "${DEV_SECRETS[@]}"; do
        if ! check_secret "$secret" "$NAMESPACE"; then
            MISSING_SECRETS+=("$secret")
        fi
    done
    
    if [ ${#MISSING_SECRETS[@]} -gt 0 ]; then
        echo ""
        echo "⚠️  Missing secrets in namespace $NAMESPACE:"
        for secret in "${MISSING_SECRETS[@]}"; do
            echo "   - $secret"
        done
        echo ""
        echo "Create missing secrets before deployment:"
        echo ""
        echo "# Azure Storage"
        echo "kubectl create secret generic azure-storage-dev-connection \\"
        echo "  --from-literal=connectionString=\"DefaultEndpointsProtocol=https;AccountName=...\" \\"
        echo "  -n $NAMESPACE"
        echo ""
        echo "# Azure Service Bus"
        echo "kubectl create secret generic azure-servicebus-dev-connection \\"
        echo "  --from-literal=connectionString=\"Endpoint=sb://...;SharedAccessKey=...\" \\"
        echo "  -n $NAMESPACE"
        echo ""
        echo "# Application Insights"
        echo "kubectl create secret generic application-insights-dev-connection \\"
        echo "  --from-literal=connectionString=\"InstrumentationKey=...;IngestionEndpoint=...\" \\"
        echo "  -n $NAMESPACE"
        echo ""
        read -p "Continue anyway? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
fi

# Build helm command
HELM_CMD="helm"
if [ -n "$UPGRADE" ]; then
    HELM_CMD="$HELM_CMD upgrade"
else
    HELM_CMD="$HELM_CMD install"
fi

HELM_CMD="$HELM_CMD $RELEASE_NAME $CHART_PATH"
HELM_CMD="$HELM_CMD -f $VALUES_FILE"
HELM_CMD="$HELM_CMD -n $NAMESPACE"

if [ -z "$UPGRADE" ]; then
    HELM_CMD="$HELM_CMD --create-namespace"
fi

if [ -n "$DRY_RUN" ]; then
    HELM_CMD="$HELM_CMD $DRY_RUN --debug"
fi

echo "Executing: $HELM_CMD"
echo ""

# Execute helm command
eval $HELM_CMD

if [ -z "$DRY_RUN" ]; then
    echo ""
    echo "=== Deployment Status ==="
    kubectl get deployments -l app.kubernetes.io/name=bioanalyzer-event-handlers -n "$NAMESPACE"
    echo ""
    kubectl get pods -l app.kubernetes.io/name=bioanalyzer-event-handlers -n "$NAMESPACE"
    
    echo ""
    echo "=== Next Steps ==="
    echo "Monitor deployment:"
    echo "  kubectl get pods -n $NAMESPACE -w"
    echo ""
    echo "Check logs:"
    echo "  kubectl logs -f deployment/$RELEASE_NAME -n $NAMESPACE"
    echo ""
    echo "Monitor Azure Functions execution:"
    echo "  kubectl logs -f deployment/$RELEASE_NAME -n $NAMESPACE | grep -E \"(Message ID|downloading|uploaded)\""
    echo ""
    if [[ "$ENVIRONMENT" == "prod" ]]; then
        echo "Check autoscaling (if enabled):"
        echo "  kubectl get hpa -n $NAMESPACE"
        echo ""
        echo "Monitor resource usage:"
        echo "  kubectl top pods -n $NAMESPACE"
    fi
    
    echo "Access Application Insights for detailed monitoring and troubleshooting."
    
    echo ""
    echo "🎉 Deployment completed successfully!"
    echo ""
    echo "The EventHandlers will now process messages from:"
    if [[ "$ENVIRONMENT" == "dev" ]]; then
        echo "  📥 Queue: download-document-request-dev"
        echo "  📤 Topic: document-downloaded-dev"
        echo "  📁 Container: downloaded-literature-dev"
    else
        echo "  📥 Queue: download-document-request"
        echo "  📤 Topic: document-downloaded"
        echo "  📁 Container: downloaded-literature"
    fi
fi