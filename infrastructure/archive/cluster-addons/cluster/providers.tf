provider "azuread" {
  tenant_id = var.tenant_id
}

provider "azurerm" {
  alias = "prod-env"
  features {}
  subscription_id = var.prod_subscription_id
}

provider "azurerm" {
  alias = "demo-env"
  features {}
  subscription_id = var.demo_subscription_id
}
provider "kubernetes" {

  config_path    = var.kube_config_path
  config_context = var.kube_config_context
}
