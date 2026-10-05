

resource "azurerm_resource_group" "bio_applications" {
  provider = azurerm.prod-env
  name     = join("-", ["rg", var.namespace, var.environment, var.location_abbreviation])
  location = var.location
  tags     = local.tags
}


data "azuread_group" "cluster_admin_group" {
  display_name = var.admin_group_name
}

# Use the aks-cluster module to create an AKS cluster
module "aks_cluster" {
  source                = "../modules/aks-cluster"
  providers             = { azurerm = azurerm.prod-env }
  namespace             = var.namespace
  environment           = var.environment
  location              = var.location
  location_abbreviation = var.location_abbreviation
  resource_group_name   = azurerm_resource_group.bio_applications.name
  vnet_address_space    = var.vnet_address_space
  tags                  = local.tags
  default_node_pool     = local.default_node_pool
  node_pools            = local.node_pools
  k8s_version           = var.k8s_version
  authorized_ips        = var.authorized_ips
  admin_group_id        = data.azuread_group.cluster_admin_group.object_id
}

# # Use the container-apps-dedicated module to create a Container Apps Environment
# module "container_apps_dedicated" {
#   source                  = "../modules/container-apps-dedicated"
#   providers               = { azurerm = azurerm.prod-env }
#   namespace               = var.namespace
#   environment             = var.environment
#   location                = var.location
#   location_abbreviation   = var.location_abbreviation
#   resource_group_name     = azurerm_resource_group.bio_applications.name
#   virtual_network_name    = module.aks_cluster.virtual_network_name
#   subnet_address_prefixes = var.container_apps_subnet_address_prefixes
#   maximum_vm_count        = var.container_apps_maximum_vm_count
#   minimum_vm_count        = var.container_apps_minimum_vm_count
#   tags                    = local.tags
# }


module "container_apps_consumption" {
  source                  = "../modules/container-apps-consumption"
  providers               = { azurerm = azurerm.prod-env }
  namespace               = var.namespace
  environment             = join("-", [var.environment, "private"])
  location                = var.location
  location_abbreviation   = var.location_abbreviation
  resource_group_name     = azurerm_resource_group.bio_applications.name
  virtual_network_name    = module.aks_cluster.virtual_network_name
  subnet_address_prefixes = var.container_apps_consumption_subnet_address_prefixes
  tags                    = local.tags
  internal_load_balancer_enabled = false
}