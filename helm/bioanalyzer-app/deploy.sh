#!/bin/bash
set -e

# BioAnalyzer App Deployment Script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHART_PATH="$SCRIPT_DIR"

# Default values
ENVIRONMENT="dev"
NAMESPACE=""
RELEASE_NAME="bioanalyzer-app"
DRY_RUN=""
UPGRADE=""

usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Deploy BioAnalyzer App using Helm

Options:
    -e, --environment ENV    Environment (dev|prod) [default: dev]
    -n, --namespace NS       Kubernetes namespace [default: bioanalyzer-ENV]
    -r, --release NAME       Helm release name [default: bioanalyzer-app]
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
    - Docker image dmaxim/bioanalyzer-app available
    - Azure Service Bus secrets created (if not using managed identity)

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
    NAMESPACE="bioanalyzer-$ENVIRONMENT"
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

echo "=== BioAnalyzer App Deployment ==="
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

echo "=================================="

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

# For production, check if secrets exist
if [[ "$ENVIRONMENT" == "prod" && -z "$DRY_RUN" ]]; then
    echo "Checking production prerequisites..."
    
    # Check if managed identity is configured or secrets exist
    # This is a basic check - adjust based on your setup
    if ! kubectl get secret azure-servicebus-secrets -n "$NAMESPACE" &> /dev/null; then
        echo "Warning: Azure Service Bus secrets not found in namespace $NAMESPACE"
        echo "Make sure you have either:"
        echo "  1. Configured Azure Managed Identity and workload identity"
        echo "  2. Created the required secrets with connection strings"
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
    kubectl get pods -l app.kubernetes.io/name=bioanalyzer-app -n "$NAMESPACE"
    echo ""
    kubectl get services -l app.kubernetes.io/name=bioanalyzer-app -n "$NAMESPACE"
    
    if [[ "$ENVIRONMENT" == "prod" ]]; then
        echo ""
        kubectl get ingress -l app.kubernetes.io/name=bioanalyzer-app -n "$NAMESPACE"
    fi
    
    echo ""
    echo "=== Next Steps ==="
    echo "Check deployment status:"
    echo "  kubectl get pods -n $NAMESPACE -w"
    echo ""
    echo "View logs:"
    echo "  kubectl logs -f deployment/$RELEASE_NAME -n $NAMESPACE"
    echo ""
    echo "Access application (if using port-forward):"
    echo "  kubectl port-forward service/$RELEASE_NAME 8080:80 -n $NAMESPACE"
    echo "  Then visit: http://localhost:8080"
fi