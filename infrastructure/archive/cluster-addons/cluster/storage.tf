resource "azurerm_storage_account" "bio_applications" {
  provider                 = azurerm.prod-env
  name                     = var.storage_account_name
  resource_group_name      = azurerm_resource_group.bio_applications.name
  location                 = azurerm_resource_group.bio_applications.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  tags = {
    environment = var.environment
    project     = "bio-applications"
  }
}

resource "azurerm_storage_container" "uploads_container" {
  provider              = azurerm.prod-env
  name                  = "knowledgeuploads"
  storage_account_id    = azurerm_storage_account.bio_applications.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "chunks_container" {
  provider              = azurerm.prod-env
  name                  = "knowledgechunks"
  storage_account_id    = azurerm_storage_account.bio_applications.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "embeddings_container" {
  provider              = azurerm.prod-env
  name                  = "knowledgeembeddings"
  storage_account_id    = azurerm_storage_account.bio_applications.id
  container_access_type = "private"
}

## Grant Blob Data Contributor role to specified users
resource "azurerm_role_assignment" "blob_data_contributor_assignments" {
  provider             = azurerm.prod-env
  for_each             = local.blob_data_contributors
  scope                = azurerm_storage_account.bio_applications.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = each.value.object_id
}

# resource "azurerm_role_assignment" "cae_blob_data_contributor" {
#   provider             = azurerm.prod-env
#   scope                = azurerm_storage_account.bio_applications.id
#   role_definition_name = "Storage Blob Data Contributor"
#   principal_id         = module.container_apps_dedicated.primary_identity_principal_id
# }