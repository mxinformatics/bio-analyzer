# Azure Entra ID app registrations for BioAnalyzer authentication.
# Layout:
#   - research_api  : protected API (JWT audience / scopes / app role)
#   - bioanalyzer_ui: Blazor interactive server OIDC client
#   - chat_ui       : Next.js NextAuth (Azure AD) client
#   - research_daemon: service-to-service client credentials (EventHandlers, etc.)

data "azuread_client_config" "current" {}

data "azuread_application_published_app_ids" "well_known" {}

data "azuread_service_principal" "msgraph" {
  client_id = data.azuread_application_published_app_ids.well_known.result["MicrosoftGraph"]
}

resource "random_uuid" "research_api_scope_access_as_user" {}
resource "random_uuid" "research_api_app_role_access" {}

locals {
  research_api_app_id_uri  = "api://bioanalyzer-research-api-${var.environment}"
  research_api_scope_value = "access_as_user"
  research_api_scope_full  = "${local.research_api_app_id_uri}/${local.research_api_scope_value}"

  # Microsoft Graph delegated permission IDs (stable, resolved from tenant SP).
  msgraph_openid         = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["openid"]
  msgraph_profile        = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["profile"]
  msgraph_email          = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["email"]
  msgraph_offline_access = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["offline_access"]
  msgraph_user_read      = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["User.Read"]

  bioanalyzer_ui_redirect_uris = distinct(concat(
    [
      # launchSettings https profile
      "https://localhost:7103/signin-oidc",
      "http://localhost:7103/signin-oidc",
      # launchSettings http profile / Aspire often uses 5035
      "https://localhost:5035/signin-oidc",
      "http://localhost:5035/signin-oidc",
    ],
    var.bioanalyzer_ui_redirect_uris
  ))

  chat_ui_redirect_uris = distinct(concat(
    [
      "http://localhost:3000/api/auth/callback/azure-ad",
      "https://localhost:3000/api/auth/callback/azure-ad",
    ],
    var.chat_ui_redirect_uris
  ))
}

# -----------------------------------------------------------------------------
# Research.Api — resource application (token audience)
# -----------------------------------------------------------------------------

resource "azuread_application" "research_api" {
  display_name     = join("-", ["app", "bioanalyzer-research-api", var.environment, var.location_abbreviation])
  owners           = [data.azuread_client_config.current.object_id]
  sign_in_audience = "AzureADMyOrg"
  identifier_uris  = [local.research_api_app_id_uri]

  api {
    mapped_claims_enabled          = true
    requested_access_token_version = 2

    oauth2_permission_scope {
      admin_consent_description  = "Allow the application to access BioAnalyzer Research API on behalf of the signed-in user."
      admin_consent_display_name = "Access BioAnalyzer Research API"
      user_consent_description   = "Allow the application to access BioAnalyzer Research API on your behalf."
      user_consent_display_name  = "Access BioAnalyzer Research API"
      enabled                    = true
      id                         = random_uuid.research_api_scope_access_as_user.result
      type                       = "User"
      value                      = local.research_api_scope_value
    }
  }

  app_role {
    allowed_member_types = ["Application"]
    description          = "Allows daemon / service clients to call BioAnalyzer Research API."
    display_name         = "Research API access"
    enabled              = true
    id                   = random_uuid.research_api_app_role_access.result
    value                = "Research.Access"
  }
}

resource "azuread_service_principal" "research_api" {
  client_id                    = azuread_application.research_api.client_id
  app_role_assignment_required = false
  owners                       = [data.azuread_client_config.current.object_id]
}

# -----------------------------------------------------------------------------
# Blazor BioAnalyzer App — interactive OIDC client
# -----------------------------------------------------------------------------

resource "azuread_application" "bioanalyzer_ui" {
  display_name     = join("-", ["app", "bioanalyzer-ui", var.environment, var.location_abbreviation])
  owners           = [data.azuread_client_config.current.object_id]
  sign_in_audience = "AzureADMyOrg"

  web {
    redirect_uris = local.bioanalyzer_ui_redirect_uris
    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = true
    }
  }

  required_resource_access {
    resource_app_id = data.azuread_application_published_app_ids.well_known.result["MicrosoftGraph"]

    resource_access {
      id   = local.msgraph_openid
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_profile
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_email
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_offline_access
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_user_read
      type = "Scope"
    }
  }

  required_resource_access {
    resource_app_id = azuread_application.research_api.client_id

    resource_access {
      id   = random_uuid.research_api_scope_access_as_user.result
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "bioanalyzer_ui" {
  client_id                    = azuread_application.bioanalyzer_ui.client_id
  app_role_assignment_required = true
  owners                       = [data.azuread_client_config.current.object_id]
}

resource "azuread_application_password" "bioanalyzer_ui" {
  application_id = azuread_application.bioanalyzer_ui.id
  display_name   = "BioAnalyzer UI client secret"
  end_date       = timeadd(timestamp(), "8760h")

  lifecycle {
    ignore_changes = [end_date]
  }
}

resource "azuread_application_pre_authorized" "research_api_bioanalyzer_ui" {
  application_id       = azuread_application.research_api.id
  authorized_client_id = azuread_application.bioanalyzer_ui.client_id
  permission_ids       = [random_uuid.research_api_scope_access_as_user.result]
}

# -----------------------------------------------------------------------------
# Chat UI (NextAuth Azure AD) — confidential web client
# -----------------------------------------------------------------------------

resource "azuread_application" "chat_ui" {
  display_name     = join("-", ["app", "bioanalyzer-chat-ui", var.environment, var.location_abbreviation])
  owners           = [data.azuread_client_config.current.object_id]
  sign_in_audience = "AzureADMyOrg"

  web {
    redirect_uris = local.chat_ui_redirect_uris
    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = true
    }
  }

  required_resource_access {
    resource_app_id = data.azuread_application_published_app_ids.well_known.result["MicrosoftGraph"]

    resource_access {
      id   = local.msgraph_openid
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_profile
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_email
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_offline_access
      type = "Scope"
    }
    resource_access {
      id   = local.msgraph_user_read
      type = "Scope"
    }
  }

  required_resource_access {
    resource_app_id = azuread_application.research_api.client_id

    resource_access {
      id   = random_uuid.research_api_scope_access_as_user.result
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "chat_ui" {
  client_id                    = azuread_application.chat_ui.client_id
  app_role_assignment_required = true
  owners                       = [data.azuread_client_config.current.object_id]
}

resource "azuread_application_password" "chat_ui" {
  application_id = azuread_application.chat_ui.id
  display_name   = "BioAnalyzer Chat UI client secret"
  end_date       = timeadd(timestamp(), "8760h")

  lifecycle {
    ignore_changes = [end_date]
  }
}

resource "azuread_application_pre_authorized" "research_api_chat_ui" {
  application_id       = azuread_application.research_api.id
  authorized_client_id = azuread_application.chat_ui.client_id
  permission_ids       = [random_uuid.research_api_scope_access_as_user.result]
}

# -----------------------------------------------------------------------------
# Daemon / service client (EventHandlers → Research.Api client credentials)
# -----------------------------------------------------------------------------

resource "azuread_application" "research_daemon" {
  display_name     = join("-", ["app", "bioanalyzer-research-daemon", var.environment, var.location_abbreviation])
  owners           = [data.azuread_client_config.current.object_id]
  sign_in_audience = "AzureADMyOrg"

  required_resource_access {
    resource_app_id = azuread_application.research_api.client_id

    resource_access {
      id   = random_uuid.research_api_app_role_access.result
      type = "Role"
    }
  }
}

resource "azuread_service_principal" "research_daemon" {
  client_id = azuread_application.research_daemon.client_id
  owners    = [data.azuread_client_config.current.object_id]
}

resource "azuread_application_password" "research_daemon" {
  application_id = azuread_application.research_daemon.id
  display_name   = "BioAnalyzer Research daemon client secret"
  end_date       = timeadd(timestamp(), "8760h")

  lifecycle {
    ignore_changes = [end_date]
  }
}

resource "azuread_app_role_assignment" "research_daemon_api_access" {
  app_role_id         = random_uuid.research_api_app_role_access.result
  principal_object_id = azuread_service_principal.research_daemon.object_id
  resource_object_id  = azuread_service_principal.research_api.object_id
}

# -----------------------------------------------------------------------------
# User access group (default user assignment for interactive apps)
# -----------------------------------------------------------------------------

resource "azuread_group" "bioanalyzer_users" {
  display_name     = join("-", ["Bioanalyzer", "Users", var.environment])
  owners           = [data.azuread_client_config.current.object_id]
  security_enabled = true
  mail_enabled     = false
}

resource "azuread_group_member" "bioanalyzer_users" {
  for_each         = local.bioanalyzer_app_group_users.users
  group_object_id  = azuread_group.bioanalyzer_users.object_id
  member_object_id = each.value.object_id
}

resource "azuread_app_role_assignment" "bioanalyzer_ui_default_access" {
  app_role_id         = "00000000-0000-0000-0000-000000000000"
  principal_object_id = azuread_group.bioanalyzer_users.object_id
  resource_object_id  = azuread_service_principal.bioanalyzer_ui.object_id
}

resource "azuread_app_role_assignment" "chat_ui_default_access" {
  app_role_id         = "00000000-0000-0000-0000-000000000000"
  principal_object_id = azuread_group.bioanalyzer_users.object_id
  resource_object_id  = azuread_service_principal.chat_ui.object_id
}

# -----------------------------------------------------------------------------
# Key Vault secrets for runtime configuration
# -----------------------------------------------------------------------------

# Shared tenant (used by Blazor App AzureAd section and others).
resource "azurerm_key_vault_secret" "azuread_tenant_id" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "AzureAd--TenantId"
  value        = var.tenant_id
}

# IMPORTANT: AzureAd--ClientId must be the *interactive UI* app (has reply URLs).
# Research.Api resource app IDs live under ResearchApi--AzureAd--* so they are not
# accidentally used as the Blazor OIDC client (AADSTS500113: no reply address).
resource "azurerm_key_vault_secret" "bioanalyzer_ui_client_id" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "AzureAd--ClientId"
  value        = azuread_application.bioanalyzer_ui.client_id
}

resource "azurerm_key_vault_secret" "bioanalyzer_ui_client_secret" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "AzureAd--ClientSecret"
  value        = azuread_application_password.bioanalyzer_ui.value
  content_type = "password"
}

resource "azurerm_key_vault_secret" "bioanalyzer_ui_client_id_alias" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "BioAnalyzerUI--AzureAd--ClientId"
  value        = azuread_application.bioanalyzer_ui.client_id
}

resource "azurerm_key_vault_secret" "bioanalyzer_ui_client_secret_alias" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "BioAnalyzerUI--AzureAd--ClientSecret"
  value        = azuread_application_password.bioanalyzer_ui.value
  content_type = "password"
}

resource "azurerm_key_vault_secret" "research_api_azuread_client_id" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "ResearchApi--AzureAd--ClientId"
  value        = azuread_application.research_api.client_id
}

resource "azurerm_key_vault_secret" "research_api_azuread_audience" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "ResearchApi--AzureAd--Audience"
  value        = local.research_api_app_id_uri
}

# Also publish under AzureAd--Audience for Research.Api JWT validation when it binds AzureAd section.
resource "azurerm_key_vault_secret" "research_api_azuread_audience_legacy" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "AzureAd--Audience"
  value        = local.research_api_app_id_uri
}

resource "azurerm_key_vault_secret" "azuread_research_api_scope" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "AzureAd--ResearchApiScope"
  value        = local.research_api_scope_full
}

resource "azurerm_key_vault_secret" "chat_ui_client_id" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "ChatUI--AzureAd--ClientId"
  value        = azuread_application.chat_ui.client_id
}

resource "azurerm_key_vault_secret" "chat_ui_client_secret" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "ChatUI--AzureAd--ClientSecret"
  value        = azuread_application_password.chat_ui.value
  content_type = "password"
}

resource "azurerm_key_vault_secret" "research_daemon_client_id" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "ResearchDaemon--AzureAd--ClientId"
  value        = azuread_application.research_daemon.client_id
}

resource "azurerm_key_vault_secret" "research_daemon_client_secret" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "ResearchDaemon--AzureAd--ClientSecret"
  value        = azuread_application_password.research_daemon.value
  content_type = "password"
}

resource "azurerm_key_vault_secret" "research_api_scope" {
  provider     = azurerm.mxinfo-prod
  key_vault_id = azurerm_key_vault.main.id
  name         = "ResearchApi--Scope"
  value        = local.research_api_scope_full
}
