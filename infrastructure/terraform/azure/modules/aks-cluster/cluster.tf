#--- modules/aks-cluster/cluster.tf ---#
resource "azurerm_kubernetes_cluster" "aks-cluster" {
  name                            = join("-", ["aks", var.namespace, var.environment, var.location_abbreviation])
  location                        = var.location
  resource_group_name             = var.resource_group_name
  dns_prefix                      = join("-", ["aks", var.namespace, var.environment])
  kubernetes_version              = var.k8s_version

  api_server_access_profile {
    authorized_ip_ranges = var.authorized_ips
  }

  oidc_issuer_enabled = true
  private_cluster_enabled = false
  workload_identity_enabled = true
  # linux_profile {
  #   admin_username = var.admin_user_name

  #   ssh_key {
  #     key_data = var.ssh_key
  #   }
  # }

  workload_autoscaler_profile {
    keda_enabled = true
    vertical_pod_autoscaler_enabled = false
  }
  default_node_pool {
    
    name            = var.default_node_pool.name
    node_count      = var.default_node_pool.node_count
    vm_size         = var.default_node_pool.vm_size
    os_disk_size_gb = 60
    max_pods        = var.default_node_pool.max_pod_count
    type            = "VirtualMachineScaleSets"
    vnet_subnet_id  = azurerm_subnet.default_node_pool.id

    upgrade_settings {
      max_surge = "10%"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  http_application_routing_enabled = false

  key_vault_secrets_provider {
    secret_rotation_enabled = false
  }

  network_profile {
    network_plugin    = "azure"
    network_policy    = "azure"
    load_balancer_sku = "standard"
  }

  service_mesh_profile {
    external_ingress_gateway_enabled = true
    internal_ingress_gateway_enabled = false
    mode = "Istio"
    revisions = [
      "asm-1-26"
    ]
  }

  role_based_access_control_enabled = true

  azure_active_directory_role_based_access_control {
    admin_group_object_ids = [
      var.admin_group_id
    ]
    azure_rbac_enabled = true
  }
  tags = var.tags

  upgrade_override {
    force_upgrade_enabled = false
  }
  lifecycle {
    ignore_changes = [
      api_server_access_profile[0].authorized_ip_ranges
    ]
  }
}


resource "azurerm_kubernetes_cluster_node_pool" "secondary" {
  for_each              = var.node_pools
  name                  = each.value.name
  kubernetes_cluster_id = azurerm_kubernetes_cluster.aks-cluster.id
  vm_size               = each.value.vm_size
  temporary_name_for_rotation = "${each.value.name}temp"
  node_count            = each.value.node_count
  vnet_subnet_id        = azurerm_subnet.node_pool[each.key].id
  max_pods = each.value.max_pod_count

  mode = "User"
  tags = var.tags

  lifecycle {
    ignore_changes = [
      upgrade_settings
    ]
  }
}

