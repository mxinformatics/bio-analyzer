#--- modules/aks-cluster/variables.tf ---#


variable "namespace" {
  type        = string
  description = "Namespace for resource names"
}

variable "environment" {
  type        = string
  description = "Environment used in resource names"
}

variable "location" {
  type        = string
  description = "Azure Region for resources"
}

variable "location_abbreviation" {
  type        = string
  description = "Azure Region abbreviation used in resource names"
}


variable "resource_group_name" {
  type        = string
  description = "Resource Group for resources"
}

variable "vnet_address_space" {
  type        = list(any)
  description = "CIDR address space for the virtual network"
}


variable "tags" {
  description = "Tags to attach to the resources"
}

variable "default_node_pool" {
  description = "Configuration for the default node pool"
}

variable "node_pools" {
  description = "Configuration for additional node pools"
}


variable "k8s_version" {
  type        = string
  description = "Kubernetes version to use for the cluster"
}

variable "authorized_ips" {
  type        = list(string)
  description = "List of authorized ips for API access"
  sensitive   = true
}

variable "admin_group_id" {
  type        = string
  description = "The Azure AD group ID for the admin group"
  
}