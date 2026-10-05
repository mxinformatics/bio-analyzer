
# data "azurerm_key_vault" "csi_key_vault" {
#   name                = var.csi_key_vault_name
#   resource_group_name = var.cluster_resource_group_name
# }


# resource "azurerm_user_assigned_identity" "bioanalyzer_csi" {
#   location            = var.cluster_location
#   name                = join("-", [var.csi_managed_identity_name, var.environment, var.location_abbreviation])
#   resource_group_name = var.cluster_resource_group_name
# }


# resource "kubernetes_service_account" "bioanalyzer_csi" {
#   metadata {
#     name      = var.csi_managed_identity_name
#     namespace = var.kubernetes_namespace
#     annotations = {
#       "azure.workload.identity/client-id" = azurerm_user_assigned_identity.bioanalyzer_csi.client_id
#       "azure.workload.identity/tenant-id" = var.tenant_id
#     }
#   }

#   lifecycle {
#     ignore_changes = [metadata[0].annotations,
#       metadata[0].labels
#     ]
#   }

# }

# data "azurerm_aks_cluster" "cluster" {
#   name                = var.cluster_name
#   resource_group_name = var.cluster_resource_group_name
# }

# resource "azurerm_federated_identity_credential" "bioanalyzer_csi" {
#   name                = azurerm_user_assigned_identity.bioanalyzer_csi.name
#   resource_group_name = var.cluster_resource_group_name
#   audience            = ["api://AzureADTokenExchange"]
#   issuer              = data.azurerm_aks_cluster.cluster.oidc_issuer_url
#   parent_id           = azurerm_user_assigned_identity.bioanalyzer_csi.id
#   subject             = join(":", ["system:serviceaccount", var.kubernetes_namespace, var.csi_managed_identity_name])
# }

# resource "azurerm_role_assignment" "key_vault_secret_reader_csi" {
#   scope              = data.azurerm_key_vault.csi_key_vault.id
#  role_definition_name = "Key Vault Secrets User"
#   principal_id       = azurerm_user_assigned_identity.bioanalyzer_csi.principal_id
# }

# resource "azurerm_role_assignment" "key_vault_role_csi" {
#   for_each           = local.key_vault_secret_managers
#   scope              = data.azurerm_key_vault.csi_key_vault.id
#   role_definition_name = "Key Vault Secret Officer"
#   principal_id       = each.value.object_id
# }