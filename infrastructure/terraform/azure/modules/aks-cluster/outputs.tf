#--- modules/aks-cluster/outputs.tf ---#

output "virtual_network_name" {
  description = "Name of the virtual network created for the AKS cluster"
  value       = azurerm_virtual_network.cluster_network.name
}


output "oidc_issuer_url" {
  description = "OIDC Issuer URL for the AKS cluster"
  value       = azurerm_kubernetes_cluster.aks-cluster.oidc_issuer_url
}

output "cluster_identity_principal_id" {
  description = "The Principal ID of the Cluster Managed Identity"
  value       = azurerm_kubernetes_cluster.aks-cluster.identity[0].principal_id
}