
variable "resource_group_name" {
  description = "The name of the resource group where the Container Apps Environment will be deployed."
  type        = string
}

variable "location" {
  description = "The Azure location where resources will be deployed."
  type        = string
}

variable "namespace" {
  description = "The namespace for resource naming."
  type        = string
}

variable "environment" {
  description = "The environment for resource naming (e.g., dev, prod)."
  type        = string
}

variable "location_abbreviation" {
  description = "Abbreviation for the Azure location (e.g., 'eus' for East US)."
  type        = string
}

variable "tags" {
  description = "A map of tags to assign to the resources."
  type        = map(string)
  default     = {}
}

variable "virtual_network_name" {
  description = "The name of the virtual network."
  type        = string
}

variable "subnet_address_prefixes" {
  description = "The address prefixes for the subnet."
  type        = list(string)
}

variable "internal_load_balancer_enabled" {
  description = "Enable internal load balancer for the Container Apps Environment."
  type        = bool
  default     = true
}