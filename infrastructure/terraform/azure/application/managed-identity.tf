# resource "azurerm_user_assigned_identity" "bioanalyzer" {
#   location            = azurerm_resource_group.bioanalyzer.location
#   name                = join("-", [var.service_account_name, var.environment, var.location_abbreviation])
#   resource_group_name = azurerm_resource_group.bioanalyzer.name
# }


# resource "kubernetes_service_account" "bioanalyzer" {
#   metadata {
#     name      = var.service_account_name
#     namespace = var.kubernetes_namespace
#     annotations = {
#       "azure.workload.identity/client-id" = azurerm_user_assigned_identity.bioanalyzer.client_id
#       "azure.workload.identity/tenant-id" = var.tenant_id
#     }
#   }

#   lifecycle {
#     ignore_changes = [metadata[0].annotations,
#       metadata[0].labels
#     ]
#   }

# }

# resource "azurerm_federated_identity_credential" "bioanalyzer" {
#   name                = azurerm_user_assigned_identity.bioanalyzer.name
#   resource_group_name = azurerm_resource_group.bioanalyzer.name
#   audience            = ["api://AzureADTokenExchange"]
#   issuer              = data.azurerm_kubernetes_cluster.aks.oidc_issuer_url
#   parent_id           = azurerm_user_assigned_identity.bioanalyzer.id
#   subject             = join(":", ["system:serviceaccount", var.kubernetes_namespace, var.service_account_name])
# }

# resource "azurerm_role_assignment" "key_vault_secret_reader" {
#   scope              = azurerm_key_vault.bioanalyzer.id
#   role_definition_id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/providers/Microsoft.Authorization/roleDefinitions/4633458b-17de-408a-b874-0445c86b69e6"
#   principal_id       = azurerm_user_assigned_identity.bioanalyzer.principal_id
# }

# data "azurerm_search_service" "ai_search" {
#   name                = var.azure_ai_search_service_name
#   resource_group_name = var.azure_ai_search_resource_group
# }

# data "azurerm_role_definition" "search_index_data_reader" {
#   name = "Search Index Data Reader"
#   scope = data.azurerm_search_service.ai_search.id
# }

# data "azurerm_role_definition" "search_index_data_contributor" {
#   name = "Search Index Data Contributor"
#   scope = data.azurerm_search_service.ai_search.id
# }

# resource "azurerm_role_assignment" "search_index_data_reader" {
#   scope              = data.azurerm_search_service.ai_search.id
#   role_definition_id = data.azurerm_role_definition.search_index_data_reader.id
#   principal_id       = azurerm_user_assigned_identity.bioanalyzer.principal_id
# }

# resource "azurerm_role_assignment" "search_index_data_contributor" {
#   scope            = data.azurerm_search_service.ai_search.id
#   role_definition_id = data.azurerm_role_definition.search_index_data_contributor.id
#   principal_id       = azurerm_user_assigned_identity.bioanalyzer.principal_id
# }


# data "azurerm_role_definition" "service_bus_data_sender" {
#   name = "Azure Service Bus Data Sender"
#   scope = azurerm_servicebus_namespace.bioanalzyer.id
# }

# resource "azurerm_role_assignment" "service_bus_data_sender" {
#   scope              = azurerm_servicebus_namespace.bioanalzyer.id
#   role_definition_id = data.azurerm_role_definition.service_bus_data_sender.id
#   principal_id       = azurerm_user_assigned_identity.bioanalyzer.principal_id
# }