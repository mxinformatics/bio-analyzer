resource "azurerm_user_assigned_identity" "bio_applications" {
  provider            = azurerm.prod-env
  location            = azurerm_resource_group.bio_applications.location
  name                = join("-", [var.service_account_name, var.environment, var.location_abbreviation])
  resource_group_name = azurerm_resource_group.bio_applications.name
}


resource "kubernetes_namespace" "bio_applications" {
  metadata {
    name = var.data_store_kubernetes_namespace
  }

  lifecycle {
    ignore_changes = [metadata[0].labels]
  }
}

resource "kubernetes_service_account" "bio_applications" {
  metadata {
    name      = var.service_account_name
    namespace = var.data_store_kubernetes_namespace
    annotations = {
      "azure.workload.identity/client-id" = azurerm_user_assigned_identity.bio_applications.client_id
      "azure.workload.identity/tenant-id" = var.tenant_id
    }
  }

  lifecycle {
    ignore_changes = [metadata[0].annotations,
      metadata[0].labels
    ]
  }

}

resource "azurerm_federated_identity_credential" "bio_applications" {
  provider            = azurerm.prod-env
  name                = azurerm_user_assigned_identity.bio_applications.name
  resource_group_name = azurerm_resource_group.bio_applications.name
  audience            = ["api://AzureADTokenExchange"]
  issuer              = module.aks_cluster.oidc_issuer_url
  parent_id           = azurerm_user_assigned_identity.bio_applications.id
  subject             = join(":", ["system:serviceaccount", var.data_store_kubernetes_namespace, var.service_account_name])
}

resource "azurerm_role_assignment" "key_vault_secret_reader" {
  provider           = azurerm.prod-env
  scope              = azurerm_key_vault.bio_applications.id
  role_definition_name = "Key Vault Secrets User"
  principal_id       = azurerm_user_assigned_identity.bio_applications.principal_id
}

data "azurerm_container_registry" "private_acr" {
  provider            = azurerm.prod-env
  name                = var.acr_name
  resource_group_name = var.acr_resource_group
}

resource "azurerm_role_assignment" "private_container_registry_pull" {
  provider             = azurerm.prod-env
  scope                = data.azurerm_container_registry.private_acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.bio_applications.principal_id
}

resource "azurerm_role_assignment" "storage_blob_data_contributor" {
  provider             = azurerm.prod-env
  scope                = azurerm_storage_account.bio_applications.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.bio_applications.principal_id
}


resource "azurerm_role_assignment" "cognitive_account_user" {
  provider             = azurerm.prod-env
  scope                = azurerm_cognitive_account.bio_applications.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.bio_applications.principal_id
}

