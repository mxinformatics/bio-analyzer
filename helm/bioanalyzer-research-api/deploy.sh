#!/bin/bash

# BioAnalyzer Research API Deployment Script
# This script helps deploy the BioAnalyzer Research API using Helm

set -e

# Configuration
CHART_PATH="helm/bioanalyzer-research-api"
RELEASE_NAME="bioanalyzer-api"
NAMESPACE="bioanalyzer"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Functions
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Help function
show_help() {
    cat << EOF
BioAnalyzer Research API Deployment Script

Usage: $0 [OPTIONS]

OPTIONS:
    -h, --help              Show this help message
    -n, --namespace NAME    Kubernetes namespace (default: bioanalyzer)
    -r, --release NAME      Helm release name (default: bioanalyzer-api)
    -f, --values FILE       Values file to use (default: values.yaml)
    -d, --dry-run          Perform a dry run without installing
    -u, --upgrade          Upgrade existing installation
    --uninstall            Uninstall the release
    --lint                 Lint the Helm chart
    --template             Generate and display templates
    --production           Use production values file

EXAMPLES:
    $0                      # Basic installation with default values
    $0 --production         # Install with production configuration
    $0 -f my-values.yaml    # Install with custom values file
    $0 --dry-run           # Test the deployment without installing
    $0 --upgrade           # Upgrade existing deployment
    $0 --uninstall         # Remove the deployment

EOF
}

# Parse command line arguments
VALUES_FILE=""
DRY_RUN=false
UPGRADE=false
UNINSTALL=false
LINT=false
TEMPLATE=false
PRODUCTION=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            show_help
            exit 0
            ;;
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        -r|--release)
            RELEASE_NAME="$2"
            shift 2
            ;;
        -f|--values)
            VALUES_FILE="$2"
            shift 2
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -u|--upgrade)
            UPGRADE=true
            shift
            ;;
        --uninstall)
            UNINSTALL=true
            shift
            ;;
        --lint)
            LINT=true
            shift
            ;;
        --template)
            TEMPLATE=true
            shift
            ;;
        --production)
            PRODUCTION=true
            VALUES_FILE="${CHART_PATH}/values-production.yaml"
            shift
            ;;
        *)
            print_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Validate prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."
    
    if ! command -v helm &> /dev/null; then
        print_error "Helm is not installed or not in PATH"
        exit 1
    fi
    
    if ! command -v kubectl &> /dev/null; then
        print_error "kubectl is not installed or not in PATH"
        exit 1
    fi
    
    # Check if kubectl can connect to cluster
    if ! kubectl cluster-info &> /dev/null; then
        print_error "Cannot connect to Kubernetes cluster"
        exit 1
    fi
    
    print_success "Prerequisites check passed"
}

# Create namespace if it doesn't exist
create_namespace() {
    if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
        print_info "Creating namespace: $NAMESPACE"
        kubectl create namespace "$NAMESPACE"
        print_success "Namespace created: $NAMESPACE"
    else
        print_info "Namespace already exists: $NAMESPACE"
    fi
}

# Lint the Helm chart
lint_chart() {
    print_info "Linting Helm chart..."
    if helm lint "$CHART_PATH"; then
        print_success "Chart lint passed"
    else
        print_error "Chart lint failed"
        exit 1
    fi
}

# Generate templates
generate_templates() {
    print_info "Generating Helm templates..."
    local values_arg=""
    if [[ -n "$VALUES_FILE" ]]; then
        values_arg="-f $VALUES_FILE"
    fi
    
    helm template "$RELEASE_NAME" "$CHART_PATH" $values_arg --namespace "$NAMESPACE"
}

# Install or upgrade the chart
deploy_chart() {
    local action="install"
    local values_arg=""
    local dry_run_arg=""
    
    if [[ "$UPGRADE" == true ]]; then
        action="upgrade"
    fi
    
    if [[ -n "$VALUES_FILE" ]]; then
        values_arg="-f $VALUES_FILE"
        print_info "Using values file: $VALUES_FILE"
    fi
    
    if [[ "$DRY_RUN" == true ]]; then
        dry_run_arg="--dry-run"
        print_info "Performing dry run..."
    fi
    
    print_info "Running helm $action for release: $RELEASE_NAME"
    
    if helm "$action" "$RELEASE_NAME" "$CHART_PATH" \
        --namespace "$NAMESPACE" \
        --create-namespace \
        $values_arg \
        $dry_run_arg; then
        
        if [[ "$DRY_RUN" != true ]]; then
            print_success "Successfully ${action}ed release: $RELEASE_NAME"
            
            # Show release status
            print_info "Release status:"
            helm status "$RELEASE_NAME" --namespace "$NAMESPACE"
        else
            print_success "Dry run completed successfully"
        fi
    else
        print_error "Failed to $action release: $RELEASE_NAME"
        exit 1
    fi
}

# Uninstall the release
uninstall_chart() {
    print_info "Uninstalling release: $RELEASE_NAME"
    
    if helm uninstall "$RELEASE_NAME" --namespace "$NAMESPACE"; then
        print_success "Successfully uninstalled release: $RELEASE_NAME"
    else
        print_error "Failed to uninstall release: $RELEASE_NAME"
        exit 1
    fi
}

# Main execution
main() {
    print_info "BioAnalyzer Research API Deployment"
    print_info "Release: $RELEASE_NAME"
    print_info "Namespace: $NAMESPACE"
    
    if [[ "$PRODUCTION" == true ]]; then
        print_warning "Using PRODUCTION configuration!"
    fi
    
    check_prerequisites
    
    if [[ "$LINT" == true ]]; then
        lint_chart
        exit 0
    fi
    
    if [[ "$TEMPLATE" == true ]]; then
        generate_templates
        exit 0
    fi
    
    if [[ "$UNINSTALL" == true ]]; then
        uninstall_chart
        exit 0
    fi
    
    if [[ "$DRY_RUN" != true ]]; then
        create_namespace
    fi
    
    deploy_chart
    
    if [[ "$DRY_RUN" != true ]]; then
        print_success "Deployment completed!"
        print_info "To check the deployment status, run:"
        print_info "  kubectl get pods -n $NAMESPACE"
        print_info "  helm status $RELEASE_NAME -n $NAMESPACE"
    fi
}

# Execute main function
main