provider "azurerm" {
  features {}
  alias           = "mxinfo-prod"
  subscription_id = var.subscription_id
}
provider "azuread" {
  tenant_id = var.tenant_id
}

provider "azapi" {
  alias           = "mxinfo-prod"
  subscription_id = var.subscription_id
}