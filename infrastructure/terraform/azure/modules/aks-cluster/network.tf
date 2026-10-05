#--- modules/aks-cluster/network.tf ---#

# Create Virtual Network and Subnets for the worker node pools

resource "azurerm_virtual_network" "cluster_network" {
  name                = join("-", ["vnet", var.namespace, var.environment, var.location_abbreviation])
  location            = var.location
  resource_group_name = var.resource_group_name

  address_space = var.vnet_address_space

  tags = var.tags
}


# Create default node pool subnet

resource "azurerm_subnet" "default_node_pool" {
  name                 = join("-", ["snet", var.namespace, var.default_node_pool.name, var.environment, var.location_abbreviation])
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.cluster_network.name

  #address_prefixes = [cidrsubnet(var.vnet_address_space[0], 4, var.default_node_pool.subnet_index)]
  address_prefixes = var.default_node_pool.address_prefixes
}

# Create additional node pool subnets

resource "azurerm_subnet" "node_pool" {
  for_each             = var.node_pools
  name                 = join("-", ["snet", var.namespace, each.value.name, var.environment, var.location_abbreviation])
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.cluster_network.name

  #address_prefixes = [cidrsubnet(var.vnet_address_space[0], 4, each.value.subnet_index)]
  address_prefixes = each.value.address_prefixes
}