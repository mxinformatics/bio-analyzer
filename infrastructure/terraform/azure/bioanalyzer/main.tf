
# Create Azure Resource Group
resource "azurerm_resource_group" "main" {
  provider = azurerm.mxinfo-prod
  name     = join("-", ["rg", var.namespace, var.environment, var.location_abbreviation])
  location = var.location
  tags     = local.tags
}
# Create Azure Key Vault
resource "azurerm_key_vault" "main" {
  provider                    = azurerm.mxinfo-prod
  name                        = join("-", [var.key_vault_name, var.environment])
  location                    = azurerm_resource_group.main.location
  resource_group_name         = azurerm_resource_group.main.name
  tenant_id                   = var.tenant_id
  sku_name                    = "standard"
  purge_protection_enabled    = true
  enabled_for_disk_encryption = false
  soft_delete_retention_days  = 10
  tags                        = local.tags
  rbac_authorization_enabled  = true
}

resource "azurerm_role_assignment" "key_vault_manager" {
  provider             = azurerm.mxinfo-prod
  for_each             = local.key_vault_secret_managers
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = each.value.object_id
}
# Create Azure Storage Account
resource "azurerm_storage_account" "main" {
  provider                 = azurerm.mxinfo-prod
  name                     = join("", [var.azure_storage_account_name, var.environment])
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  tags                     = local.tags
}

resource "azurerm_role_assignment" "storage_blob_data_contributor" {
  provider             = azurerm.mxinfo-prod
  for_each             = local.blob_storage_contributors
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = each.value.object_id
}

resource "azurerm_role_assignment" "storage_table_contributor" {
  provider             = azurerm.mxinfo-prod
  for_each             = local.blob_storage_contributors
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = each.value.object_id
}

resource "azurerm_storage_container" "bioanalyzer" {
  provider              = azurerm.mxinfo-prod
  name                  = "literature"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"

}

resource "azurerm_storage_container" "bioanalyzer_extracted" {
  provider              = azurerm.mxinfo-prod
  name                  = "extractedtext"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "blob"
}

resource "azurerm_storage_container" "bioanalyzer_embedded" {
  provider              = azurerm.mxinfo-prod
  name                  = "embeddedchunks"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "blob"
}

resource "azurerm_virtual_network" "main" {
  provider            = azurerm.mxinfo-prod
  name                = join("-", ["vnet", var.namespace, var.environment, var.location_abbreviation])
  address_space       = var.virtual_network_address_space
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

# module "container_apps_environment" {
#   source = "../modules/container-apps-consumption"
#   providers = {
#     azurerm = azurerm.mxinfo-prod
#   }
#   namespace                      = var.namespace
#   environment                    = var.environment
#   location                       = var.location
#   location_abbreviation          = var.location_abbreviation
#   resource_group_name            = azurerm_resource_group.main.name
#   virtual_network_name           = azurerm_virtual_network.main.name
#   subnet_address_prefixes        = var.container_app_subnet_address_prefixes
#   tags                           = local.tags
#   internal_load_balancer_enabled = false
# }
