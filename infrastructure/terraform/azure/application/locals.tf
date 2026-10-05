
data "azuread_user" "dan_maxim" {
  user_principal_name = "dmaxim@mxinformatics.com"
}


locals {
  tags = {
    "environment" = var.environment
  }

  key_vault_secret_managers = {
    dan_maxim = {
      object_id = data.azuread_user.dan_maxim.object_id
    }
  }
  
}