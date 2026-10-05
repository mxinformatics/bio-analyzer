
# output "container_apps_identity_principal_id" {
#   description = "The principal ID of the Container Apps Environment managed identity"
#   value       = azurerm_container_app_environment.container_apps.identity[0].principal_id
# }

output "log_analytics_workspace_id" {
  description = "The ID of the Log Analytics Workspace"
  value       = azurerm_log_analytics_workspace.container_apps.id
}