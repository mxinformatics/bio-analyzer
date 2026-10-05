#--- aks/poc-infrastructure/cert-manager.tf ---# 

resource "kubernetes_namespace" "cert_ns" {
  metadata {
    name = var.cert_manager_namespace
    labels = {
      istio-injection = "disabled"
    }
  }
}


resource "helm_release" "cert_manager" {
  name = "cert-manager"

  repository = "https://charts.jetstack.io"
  chart      = "cert-manager"
  namespace  = var.cert_manager_namespace
  version    = var.cert_manager_version
  set {
    name  = "crds.enabled"
    value = "true"
  }

  set {
    name  = "extraArgs"
    value = "{--feature-gates=ACMEHTTP01IngressPathTypeExact=false}"
  }

  set {
    name  = "podLabels.azure\\.workload\\.identity/use"
    value = "true"
  }

  set {
    name  = "serviceAccount.labels.azure\\.workload\\.identity/use"
    value = "true"
  }

  lifecycle {
    ignore_changes = all
  }
}

#https://cert-manager.io/docs/releases/release-notes/release-notes-1.18 - breaking change for Exact PathType not working with nginx Ingress


resource "kubernetes_manifest" "cluster_issuer_prod" {
  depends_on = [helm_release.cert_manager]
  manifest = {
    "apiVersion" = "cert-manager.io/v1"
    "kind"       = "ClusterIssuer"
    metadata = {
      "name" = "letsencrypt-prod"
    }
    spec = {
      "acme" = {
        "server" = "https://acme-v02.api.letsencrypt.org/directory"
        "email"  = var.cert_manager_email
        "privateKeySecretRef" = {
          "name" = "letsencrypt-prod"
        }
        "solvers" = [
          {
            "http01" = {
              "ingress" = {
                "class" = "nginx"
              }
            }
          }
        ]
      }
    }
  }
}


# Need to figure out how to format this properly - currently getting error:

# resource "kubernetes_manifest" "cluster_issuer_dns" {
#   depends_on = [helm_release.cert_manager]
#   manifest = {
#     "apiVersion" = "cert-manager.io/v1"
#     "kind"       = "ClusterIssuer"
#     metadata = {
#       "name" = "letsencrypt-prod-dns"
#     }
#     spec = {
#       acme = {
#         server = "https://acme-v02.api.letsencrypt.org/directory"
#         email  = var.cert_manager_email
#         privateKeySecretRef = {
#           name = "letsencrypt-prod-dns"
#         }
#         solvers = [
#           {
#             dns01 = {
#               azureDNS = {
#                 resourceGroupName = var.dns_identity_resource_group
#                 subscriptionID    = var.prod_subscription_id
#                 hostedZoneName    = var.dns_zone_name
#                 environment       = "AzurePublicCloud"
#                 managedIdentity   = var.dns_identity_client_id
#               }
#             }
#           }
#         ]
#       }
#     }
#   }
# }
