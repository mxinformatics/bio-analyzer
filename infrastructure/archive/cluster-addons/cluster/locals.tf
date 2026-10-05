
data "azuread_user" "dan_maxim" {
  user_principal_name = var.admin_user_principal_name
}


locals {
  tags = {
    "environment" = var.environment
    "application" = "mxinfo-bio-apps"

  }

  key_vault_secret_managers = {
    dan_maxim = {
      object_id = data.azuread_user.dan_maxim.object_id
    }
  }

  blob_data_contributors = {
    dan_maxim = {
      object_id = data.azuread_user.dan_maxim.object_id
    }
  }

  ai_users = {
    dan_maxim = {
      object_id = data.azuread_user.dan_maxim.object_id
    }
  }

  asb_topics = {
    document_downloaded = {
      name = "document-downloaded"
      queues = {
        extract_document_text = {
          name = "extract-document-text"
        }
        build_document_list = {
          name = "build-document-list"
        }
      }
    }
    text_extracted = {
      name = "text-extracted"
      queues = {
        process_extracted_text = {
          name = "process-extracted-text"
        }
      }
    }
    embeddings_genereated = {
      name = "embeddings-generated"
      queues = {
        process_embeddings = {
          name = "process-embeddings"
        }
      }
    }
    download_document = {
      name = "download-document-request"

      queues = {
        document_download_queue = {
          name = "process-document-download"
        }
      }
    }
  }

  default_node_pool = {
    name          = "ver01"
    node_count    = 1
    vm_size       = var.default_node_pool_vm_size
    subnet_index  = 1
    max_pod_count = 70
    address_prefixes = ["10.130.1.0/24"]
  }

  node_pools = {
    pool_two = {
      name          = "mx02"
      node_count    = 1
      vm_size       = var.default_node_pool_vm_size
      environment   = var.environment
      subnet_index  = 2
      max_pod_count = 70
      address_prefixes = ["10.130.2.0/24"]
    }
  }
}