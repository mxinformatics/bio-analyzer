# infrastructure/azure/variables.tf

variable "namespace" {
  type        = string
  description = "Namespace for resource names"
  default     = "mxinfo-bio"
}

variable "environment" {
  type        = string
  description = "Environment used in resource names"
  default     = "prod"
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

variable "prod_subscription_id" {
  type        = string
  description = "Azure Subscription ID for production resources"

}

variable "tenant_id" {
  type        = string
  description = "Azure Tenant ID"

}

variable "key_vault_name" {
  type        = string
  description = "Key Vault name"
  default     = "kv-mxinfo-bio-prod"
}


variable "vnet_address_space" {
  type        = list(string)
  description = "CIDR address space for the virtual network"
  default     = ["10.130.0.0/18"]
}


variable "default_node_pool_vm_size" {
  type        = string
  description = "Default VM Size"
  default     = "Standard_D4ds_V5"
}

variable "small_node_pool_vm_size" {
  type        = string
  description = "Small VM Size"
  default     = "Standard_D2ds_V5"
}


variable "k8s_version" {
  type        = string
  description = "Kubernetes version to use for the cluster"
  default     = "1.33.7"
}

variable "authorized_ips" {
  type        = list(string)
  description = "IP Addresses with access to the cluster API"
  sensitive   = true
}

variable "admin_group_name" {
  type        = string
  description = "The Azure AD group name for the admin group"

}


# Container Apps Variables
variable "container_apps_subnet_address_prefixes" {
  type        = list(string)
  description = "The address prefixes for the Container Apps subnet."
  default     = ["10.130.10.0/23"]
}

variable "container_apps_consumption_subnet_address_prefixes" {
  type        = list(string)
  description = "The address prefixes for the Container Apps Consumption subnet."
  default     = ["10.130.16.0/23"]
}

variable "container_apps_maximum_vm_count" {
  type        = number
  description = "The maximum number of VMs for the Container Apps workload profile."
  default     = 3
}

variable "container_apps_minimum_vm_count" {
  type        = number
  description = "The minimum number of VMs for the Container Apps workload profile."
  default     = 1
}


variable "cert_manager_namespace" {
  type        = string
  description = "Kubernetes namespace where cert-manager is installed"
  default     = "cert-manager"
}

variable "cert_manager_service_account_name" {
  type        = string
  description = "Kubernetes service account name for cert-manager"
  default     = "cert-manager"

}


variable "knowlege_apps_key_vault_name" {
  type        = string
  description = "Key Vault name for Knowledge Apps"
  default     = "kv-mxinfo-bio-prod"
}

variable "service_account_name" {
  type        = string
  description = "Kubernetes service account name for federated identity"
  default     = "bio-applications"
}

variable "data_store_kubernetes_namespace" {
  type        = string
  description = "Kubernetes Namespace for the service account"
  default     = "data-stores"
}

variable "kube_config_path" {
  type        = string
  description = "Path to the kubeconfig file"
  default     = "~/.kube/config"
}

variable "kube_config_context" {
  type        = string
  description = "Kube config context to use"
 

}


variable "storage_account_name" {
  type        = string
  description = "The name of the Storage Account."
  default     = "mxinfobioprodeus001"
}

variable "demo_subscription_id" {
  type        = string
  description = "Azure Subscription ID for development resources"

}

variable "acr_name" {
  type        = string
  description = "The name of the Azure Container Registry."
  default     = "mxinfo"
}

variable "acr_resource_group" {
  type        = string
  description = "The resource group of the Azure Container Registry."
  default     = "rg-mxinfo-containers"
}

## AI Foundry
variable "foundry_key_vault_name" {
  type        = string
  description = "Key Vault name for AI Foundry"
  default     = "kv-mxinfo-foundry-prod"
}

variable "admin_user_principal_name" {
  type        = string
  description = "The user principal name of the admin user"
}