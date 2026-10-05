resource "azapi_resource" "ai_foundry" {
  provider                  = azapi.mxinfo-prod
  type                      = "Microsoft.CognitiveServices/accounts@2025-06-01"
  name                      = join("-", ["aif", var.namespace, var.environment, var.location_abbreviation])
  parent_id                 = azurerm_resource_group.main.id
  location                  = var.location
  schema_validation_enabled = false

  body = {
    kind = "AIServices"
    sku = {
      name = "S0"
    }
    identity = {
      type = "SystemAssigned"
    }

    properties = {
      disableLocalAuth       = true
      allowProjectManagement = true

      customSubDomainName = join("-", ["aif", var.namespace, var.environment, var.location_abbreviation])
    }
  }
}

resource "azapi_resource" "bioanalyzer_foundry_project" {
  provider                  = azapi.mxinfo-prod
  type                      = "Microsoft.CognitiveServices/accounts/projects@2025-06-01"
  name                      = join("-", ["bioanalyzer", var.namespace, var.environment, var.location_abbreviation])
  parent_id                 = azapi_resource.ai_foundry.id
  location                  = var.location
  schema_validation_enabled = false

  body = {
    sku = {
      name = "S0"
    }
    identity = {
      type = "SystemAssigned"
    }

    properties = {
      displayName = join("-", ["bioanalyzer", var.namespace, var.environment, var.location_abbreviation])
      description = "BioAnalyzer project for the AI Foundry"
    }
  }
}


resource "azapi_resource" "bioanalyzer_foundry_deployment_gpt_5_4" {
  type      = "Microsoft.CognitiveServices/accounts/deployments@2023-05-01"
  name      = "gpt-5.4-nano"
  parent_id = azapi_resource.ai_foundry.id

  depends_on = [azapi_resource.ai_foundry]

  body = {
    sku = {
      name     = "GlobalStandard"
      capacity = 120
    }

    properties = {
      model = {
        format  = "OpenAI"
        name    = "gpt-5.4-nano"
        version = "2026-03-17"
      }
    }
  }
}

resource "azapi_resource" "bioanalyzer_foundry_deployment_text_embedding" {
  type      = "Microsoft.CognitiveServices/accounts/deployments@2023-05-01"
  name      = "text-embedding-3-small"
  parent_id = azapi_resource.ai_foundry.id

  depends_on = [azapi_resource.ai_foundry]

  body = {
    sku = {
      name     = "GlobalStandard"
      capacity = 120
    }

    properties = {
      model = {
        format = "OpenAI"
        name   = "text-embedding-3-small"
      }
    }
  }
}

# Assign role assignments for the users to access AI Foundry
resource "azurerm_role_assignment" "bioanalyzer_ai_users" {
  provider             = azurerm.mxinfo-prod
  for_each             = local.bioanalyzer_app_group_users.users
  scope                = azapi_resource.ai_foundry.id
  role_definition_name = "Foundry User"
  principal_id         = each.value.object_id
}

resource "azurerm_role_assignment" "bioanalyzer_ai_contributor" {
  provider             = azurerm.mxinfo-prod
  for_each             = local.bioanalyzer_app_group_users.users
  scope                = azapi_resource.ai_foundry.id
  role_definition_name = "Cognitive Services OpenAI Contributor"
  principal_id         = each.value.object_id
}

resource "azurerm_role_assignment" "bioanalyzer_ai_cognitive_contributor" {
  provider             = azurerm.mxinfo-prod
  for_each             = local.bioanalyzer_app_group_users.users
  scope                = azapi_resource.ai_foundry.id
  role_definition_name = "Cognitive Services Contributor"
  principal_id         = each.value.object_id
}

## Assign role assignments for the user assigned identity to access AI Foundry
resource "azurerm_role_assignment" "bioanalyzer_identity_ai_user" {
  provider             = azurerm.mxinfo-prod
  scope                = azapi_resource.ai_foundry.id
  role_definition_name = "Foundry User"
  principal_id         = azurerm_user_assigned_identity.bioanalyzer.principal_id
}
resource "azurerm_role_assignment" "bioanalyzer_identity_ai_contributor" {
  provider             = azurerm.mxinfo-prod
  scope                = azapi_resource.ai_foundry.id
  role_definition_name = "Cognitive Services OpenAI Contributor"
  principal_id         = azurerm_user_assigned_identity.bioanalyzer.principal_id
}

resource "azurerm_role_assignment" "bioanalyzer_identity_ai_cognitive_contributor" {
  provider             = azurerm.mxinfo-prod
  scope                = azapi_resource.ai_foundry.id
  role_definition_name = "Cognitive Services Contributor"
  principal_id         = azurerm_user_assigned_identity.bioanalyzer.principal_id
}