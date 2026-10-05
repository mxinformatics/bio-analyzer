
data "azuread_user" "admin_user" {
  user_principal_name = var.admin_user_principal_name
}



locals {
  tags = {
    "environment" = var.environment,
    application   = "bioanalyzer"
  }

  key_vault_secret_managers = {
    admin_user = {
      object_id = data.azuread_user.admin_user.object_id
    }
  }

  asb_managers = {
    admin_user = {
      object_id = data.azuread_user.admin_user.object_id
    }
  }


  blob_storage_contributors = {
    admin_user = {
      object_id = data.azuread_user.admin_user.object_id
    }
  }

  bioanalyzer_app_group_users = {
    users = {
      admin_user = {
        object_id = data.azuread_user.admin_user.object_id
      }
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
        start_processing = {
          name = "document-processing-requested"
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
    embeddings_generated = {
      name = "embeddings-generated"
      queues = {
        process_embeddings = {
          name = "process-embeddings"
        }
      }
    }

    download_request = {
      name = "download-document-request"
      queues = [
        {
          name = "download-document"
        }
      ]
    }

    processing_failed = {
      name = "document-processing-failed"
      queues = {
        handle_failed_processing = {
          name = "handle-failed-processing"
        }
      }
    }

    graph_ready = {
      name = "graph-ready"
      queues = {
        handle_graph_ready = {
          name = "handle-graph-ready"
        }
      }
    }
  }
}