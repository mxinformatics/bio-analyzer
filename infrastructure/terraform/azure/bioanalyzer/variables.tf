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
  default     = "kv-mxinfo-bioanalyze"
}



variable "azure_storage_account_name" {
  type        = string
  description = "Storage account name"
  default     = "bioanalyzer"
}

variable "service_account_name" {
  type        = string
  description = "K8s service account for managemed identity"
  default     = "bioanalyzer"
}


variable "admin_user_principal_name" {
  type        = string
  description = "User principal name of the admin user to assign permissions to"
  default     = ""
}

variable "virtual_network_address_space" {
  type        = list(string)
  description = "Address space for the virtual network"
  default     = ["10.0.0.0/16"]
}

variable "container_app_subnet_address_prefixes" {
  type        = list(string)
  description = "Address prefixes for the container app subnet"
  default     = ["10.0.1.0/24"]
}

variable "acr_name" {
  type        = string
  description = "Azure Container Registry name"
  default     = "mxinfo"
}

variable "acr_resource_group_name" {
  type        = string
  description = "Resource group name for the Azure Container Registry"
  default     = "rg-mxinfo-containers"
}

variable "bioanalyzer_ui_redirect_uris" {
  type        = list(string)
  description = "Additional OIDC redirect URIs for the Blazor BioAnalyzer UI (beyond localhost)."
  default     = []
}

variable "chat_ui_redirect_uris" {
  type        = list(string)
  description = "Additional NextAuth Azure AD callback URIs for bioanalyzer-chat (beyond localhost)."
  default     = []
}
