output "research_api_client_id" {
  description = "Entra application (client) ID for Research.Api JWT audience validation."
  value       = azuread_application.research_api.client_id
}

output "research_api_app_id_uri" {
  description = "Application ID URI used as JWT audience (api://...)."
  value       = local.research_api_app_id_uri
}

output "research_api_scope" {
  description = "Delegated scope clients must request (api://.../access_as_user)."
  value       = local.research_api_scope_full
}

output "bioanalyzer_ui_client_id" {
  description = "Entra client ID for the Blazor BioAnalyzer UI."
  value       = azuread_application.bioanalyzer_ui.client_id
}

output "chat_ui_client_id" {
  description = "Entra client ID for bioanalyzer-chat (NextAuth Azure AD)."
  value       = azuread_application.chat_ui.client_id
}

output "research_daemon_client_id" {
  description = "Entra client ID for daemon/service callers (EventHandlers)."
  value       = azuread_application.research_daemon.client_id
}

output "entra_tenant_id" {
  description = "Azure AD tenant ID."
  value       = var.tenant_id
}

output "key_vault_name" {
  description = "Key Vault holding Entra client secrets and AzureAd settings."
  value       = azurerm_key_vault.main.name
}
