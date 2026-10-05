# infrastructure/azure/variables.tf

variable "namespace" {
  type        = string
  description = "Namespace for resource names"
  default     = "mxinfo-bioanalyzer"
}

variable "environment" {
  type        = string
  description = "Environment used in resource names"
  default     = "poc"
}

variable "location" {
  type        = string
  description = "Azure Region for resources"
  default     = "eastus"
}

variable "location_abbreviation" {
  type        = string
  description = "Azure Region abbreviation used in resource names"
  default     = "eus"
}

variable "subscription_id" {
  type        = string
  description = "Azure Subscription ID"

}

variable "tenant_id" {
  type        = string
  description = "Azure Tenant ID"

}

variable "key_vault_name" {
  type        = string
  description = "Key Vault name"
  default     = "kv-mxinfo-bioanalyze-poc"
}



variable "azure_storage_account_name" {
  type        = string
  description = "Storage account name"
  default     = "bioanalyzerpoc"
}

variable "service_account_name" {
  type = string
  description = "K8s service account for managemed identity"
  default     = "bioanalyzer"
}

variable "kubernetes_namespace" {
  type        = string
  description = "Kubernetes namespace for service account"
  default     = "bio"
}



variable "kube_config_path" {
  type        = string
  description = "Path to kube config file"
  default     = "~/.kube/config"
}

variable "kube_config_context" {
  type        = string
  description = "Kube config context to use"
  default     = "aks-mxinfo-apps-poc-eus-admin"

}

variable "azure_ai_search_resource_group" {
  type = string
  description = "Resource group where Azure AI Search is deployed"
  
}

variable "azure_ai_search_service_name" {
  type = string
  description = "Azure AI Search service name"
  
}


# Bioanalyzer UI App
variable "bioanalyzer_ui_app_name" {
  type        = string
  description = "Bioanalyzer UI App name"
  default     = "bioanalyzer"
}

# CSI Configuration
variable "cluster_resource_group_name" {
  type        = string
  description = "Resource group where AKS cluster is deployed"
  default     = "rg-mxinfo-apps-poc-eus"
}


variable "cluster_location" {
  type        = string
  description = "Location where AKS cluster is deployed"
  default     = "eastus"
}

variable "cluster_name" {
  type        = string
  description = "AKS Cluster name"
  default     = "aks-mxinfo-bio-prod-eus"
}
variable "csi_key_vault_name" {
  type        = string
  description = "Key Vault name for CSI"
  default     = "kv-mxinfoappscsi-poc"
}

variable "csi_managed_identity_name" {
  type        = string
  description = "Managed identity name for CSI"
  default     = "mi-bioanalyzer-poc"
}


variable "chat_ui_app_name" {
  type        = string
  description = "Chat UI App name"
  default     = "bioanalyzer-chat"
}