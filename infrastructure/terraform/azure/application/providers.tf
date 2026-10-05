provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}
provider "azuread" {
  tenant_id = var.tenant_id
}

# provider "kubernetes" {

#   config_path    = var.kube_config_path
#   config_context = var.kube_config_context
# }
