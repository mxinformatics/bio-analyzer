
resource "azurerm_key_vault" "bio_applications" {
  provider                      = azurerm.prod-env
  name                          = var.knowlege_apps_key_vault_name
  location                      = azurerm_resource_group.bio_applications.location
  resource_group_name           = azurerm_resource_group.bio_applications.name
  tenant_id                     = var.tenant_id
  sku_name                      = "standard"
  soft_delete_retention_days    = 7
  rbac_authorization_enabled =    true
  public_network_access_enabled = true
  tags                          = local.tags

}

# Key Vault Secrets Officer (read/write/delete secrets)
resource "azurerm_role_assignment" "kv_secrets_officer" {
  provider           = azurerm.prod-env
  for_each           = local.key_vault_secret_managers
  scope              = azurerm_key_vault.bio_applications.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id       = each.value.object_id
}

# resource "azurerm_role_assignment" "cae_secrets_officer" {
#   provider           = azurerm.prod-env
#   scope              = azurerm_key_vault.bio_applications.id
#   role_definition_name = "Key Vault Secrets Officer"
#   principal_id       = module.container_apps_dedicated.primary_identity_principal_id
# }

# # Create Storage Account for Key Vault Diagnostics
# resource "azurerm_storage_account" "bio_applications_kv_diagnostics" {
#   provider                     = azurerm.prod-env
#   name                         = join("", [var.storage_account_name, "kvlogs"])
#   resource_group_name          = azurerm_resource_group.bio_applications.name
#   location                     = azurerm_resource_group.bio_applications.location
#   account_tier                 = "Standard"
#   account_replication_type     = "LRS"

#   min_tls_version              = "TLS1_2"

#   access_tier                  = "Hot"
#   tags                         = local.tags
# }

# # Storage Management Policy for Key Vault Diagnostics Log Retention
# resource "azurerm_storage_management_policy" "bio_applications_kv_diagnostics" {
#   provider           = azurerm.prod-env
#   storage_account_id = azurerm_storage_account.bio_applications_kv_diagnostics.id

#   rule {
#     name    = "deleteOldLogs"
#     enabled = true

#     filters {
#       blob_types = ["blockBlob"]
#     }

#     actions {
#       base_blob {
#         delete_after_days_since_modification_greater_than = 30
#       }
#     }
#   }
# }

# # Create Diagnostic Settings for Key Vault
# resource "azurerm_monitor_diagnostic_setting" "bio_applications_diagnostic_settings" {
#   provider                   = azurerm.prod-env
#   name                       = "${azurerm_key_vault.bio_applications.name}-logs"
#   target_resource_id         = azurerm_key_vault.bio_applications.id
#   log_analytics_workspace_id = module.container_apps_dedicated.log_analytics_workspace_id
#   storage_account_id         = azurerm_storage_account.bio_applications_kv_diagnostics.id


#   enabled_log {
#     category = "AuditEvent"

#   }

#   enabled_metric {
#     category = "AllMetrics"
#   }
# }

