resource "azurerm_user_assigned_identity" "bioanalyzer" {
  provider            = azurerm.mxinfo-prod
  name                = join("-", ["bioanalyzer", var.namespace, var.environment, var.location_abbreviation])
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_role_assignment" "bioanalyzer_key_vault_reader" {
  provider             = azurerm.mxinfo-prod
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.bioanalyzer.principal_id
}

data "azurerm_container_registry" "mxinfo_acr" {
  provider            = azurerm.mxinfo-prod
  name                = var.acr_name
  resource_group_name = var.acr_resource_group_name
}

resource "azurerm_role_assignment" "bioanalyzer_acr_pull" {
  provider             = azurerm.mxinfo-prod
  scope                = data.azurerm_container_registry.mxinfo_acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.bioanalyzer.principal_id
}