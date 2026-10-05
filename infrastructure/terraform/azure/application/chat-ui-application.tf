# Create an azure ad application for the search ui along with a service principal and a secret
# Need to add Graph API permissions for User.Read and openid profile 
resource "azuread_application" "chat_ui" {
  display_name     = join("-", ["app", var.chat_ui_app_name, var.environment, var.location_abbreviation])
  owners           = [data.azuread_client_config.current.object_id]
  sign_in_audience = "AzureADMyOrg"
  web {
    redirect_uris = [
      "http://localhost:3000/auth/callback",
      "http://localhost:3000/api/auth/callback/azure-ad"
    ]
    implicit_grant {
      access_token_issuance_enabled = true
      id_token_issuance_enabled     = true
    }
  }

  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000"

    resource_access {
      id   = "64a6cdd6-aab1-4aaf-94b8-3cc8405e90d0"
      type = "Scope"
    }
    resource_access {
      id   = "14dad69e-099b-42c9-810b-d002981feec1"
      type = "Scope"
    }
  }

}

resource "azuread_service_principal" "chat_ui" {
  client_id = azuread_application.chat_ui.client_id
  owners    = [data.azuread_client_config.current.object_id]
}

resource "azuread_service_principal_password" "chat_ui" {
  service_principal_id = azuread_service_principal.chat_ui.id
  end_date             = timeadd(timestamp(), "8760h")
  display_name         = "Chat UI Secret 2025"

  lifecycle {
    ignore_changes = [end_date]
  }
}

# resource "azurerm_key_vault_secret" "chat_ui_client_id" {
#   key_vault_id = azurerm_key_vault.bioanalyzer.id
#   name         = "ChatUI-AzureAD-ClientId"
#   value        = azuread_service_principal.chat_ui.client_id

# }

# resource "azurerm_key_vault_secret" "chat_ui_client_secret" {
#   key_vault_id = azurerm_key_vault.bioanalyzer.id
#   name         = "ChatUI-AzureAD-ClientSecret"
#   value        = azuread_service_principal_password.chat_ui.value
# }
