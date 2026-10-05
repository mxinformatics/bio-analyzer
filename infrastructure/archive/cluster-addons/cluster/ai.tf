# ## AI Resources - AI Foundry

resource "azurerm_key_vault" "bio_applications_foundry" {
  provider                      = azurerm.prod-env
  name                          = var.foundry_key_vault_name
  location                      = azurerm_resource_group.bio_applications.location
  resource_group_name           = azurerm_resource_group.bio_applications.name
  tenant_id                     = var.tenant_id
  sku_name                      = "standard"
  enabled_for_disk_encryption   = false
  soft_delete_retention_days    = 7
  rbac_authorization_enabled = true
  public_network_access_enabled = true

  tags = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}


resource "azurerm_role_assignment" "foundry_key_vault_role" {
  provider             = azurerm.prod-env
  for_each             = local.key_vault_secret_managers
  scope                = azurerm_key_vault.bio_applications.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value.object_id
}


resource "azurerm_ai_foundry" "bio_applications" {
  provider            = azurerm.prod-env
  name                = join("-", ["aif", var.namespace, var.environment])
  resource_group_name = azurerm_resource_group.bio_applications.name
  location            = azurerm_resource_group.bio_applications.location
  storage_account_id  = azurerm_storage_account.bio_applications.id
  key_vault_id        = azurerm_key_vault.bio_applications.id

  identity {
    type = "SystemAssigned"
  }

  tags = local.tags

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_ai_foundry_project" "bio_applications" {
  provider           = azurerm.prod-env
  name               = join("-", [var.namespace])
  location           = azurerm_resource_group.bio_applications.location
  ai_services_hub_id = azurerm_ai_foundry.bio_applications.id
  description        = "BIo Applications Project"

  tags = local.tags

  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_cognitive_account" "bio_applications" {
  provider              = azurerm.prod-env
  name                  = join("-", [var.namespace, var.environment])
  location              = azurerm_resource_group.bio_applications.location
  resource_group_name   = azurerm_resource_group.bio_applications.name
  kind                  = "OpenAI"
  sku_name              = "S0"
  custom_subdomain_name = join("-", [var.namespace, var.environment])
  fqdns                 = []

  tags = local.tags

  network_acls {
    default_action = "Allow"
    ip_rules       = []
  }
  #   lifecycle {
  #     ignore_changes = [tags]
  #   }
}


resource "azurerm_role_assignment" "foundry_ai_cognitive_role_app_identity" {
  provider             = azurerm.prod-env
  scope                = azurerm_cognitive_account.bio_applications.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_ai_foundry.bio_applications.identity[0].principal_id
}

resource "azurerm_role_assignment" "foundry_ai_cognitive_role_app_identity_cognitive_user" {
  provider             = azurerm.prod-env
  scope                = azurerm_cognitive_account.bio_applications.id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_ai_foundry.bio_applications.identity[0].principal_id
}

resource "azurerm_role_assignment" "open_ai_users" {
  provider             = azurerm.prod-env
  for_each             = local.ai_users
  scope                = azurerm_cognitive_account.bio_applications.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = each.value.object_id
}

resource "azurerm_cognitive_deployment" "open_ai_deployment" {
  provider             = azurerm.prod-env
  name                 = "gpt-4.1-mini"
  cognitive_account_id = azurerm_cognitive_account.bio_applications.id
  rai_policy_name = "Microsoft.DefaultV2"
  model {
    format = "OpenAI"
    name   = "gpt-4.1-mini"

  }
  sku {
    name = "Standard"
    capacity = 260
  }

  lifecycle {
    ignore_changes = [model[0].version]
  }
}

resource "azurerm_cognitive_deployment" "embedding_deployment" {
  provider             = azurerm.prod-env
  name                 = "text-embedding-3-small"
  cognitive_account_id = azurerm_cognitive_account.bio_applications.id
  rai_policy_name = "Microsoft.DefaultV2"

  model {
    format = "OpenAI"
    name   = "text-embedding-3-small"

  }
  sku {
    name = "Standard"
    capacity = 150
  }
  lifecycle {
    ignore_changes = [model[0].version]
  }
}

resource "azurerm_cognitive_deployment" "mini_deployment" {
  provider             = azurerm.prod-env
  name                 = "gpt-4o-mini"
  cognitive_account_id = azurerm_cognitive_account.bio_applications.id
  rai_policy_name = "Microsoft.DefaultV2"
  
  model {
    format = "OpenAI"
    name   = "gpt-4.1-mini"

  }
  sku {
    name = "Standard"
    capacity = 260
  }

  lifecycle {
    ignore_changes = [model[0].version]
  }
}