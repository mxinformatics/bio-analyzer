terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.66.0"


    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.1.0"
    }

   kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.38.0"
    }
  }

}
