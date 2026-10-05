
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.74.0"


    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.8.0"
    }

    azapi = {
      source  = "azure/azapi"
      version = "~> 2.8.0"
    }

    random = {
      source  = "hashicorp/random"
      version = "~> 3.7.0"
    }
  }

}
