output "aks_cluster_identity_principal_id" {
  description = "The Principal ID of the Cluster Managed Identity"
  value       = module.aks_cluster.cluster_identity_principal_id
}

# output "application_managed_identity_principal_id" {
#   description = "The Principal ID of the Application Managed Identity"
#   value       = azurerm_user_assigned_identity.bio_applications.principal_id
# }

# output "open_ai_endpoint" {
#   description = "The endpoint for the OpenAI Cognitive Service"
#   value       = azurerm_cognitive_account.bio_applications.endpoint
# }