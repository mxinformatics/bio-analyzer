# #--- aks/dev-infrastructure/nginx.tf ---#

# resource "kubernetes_namespace" "nginx" {
#   metadata {
#     name = var.ingress_namespace
#     labels = {
#       istio-injection = "disabled"
#     }
#   }
# }

# resource "helm_release" "nginx_ingress" {
#   name = "nginx-ingress"

#   repository = "https://kubernetes.github.io/ingress-nginx"
#   chart      = "ingress-nginx"
#   namespace  = var.ingress_namespace
#   version    = var.ingress_version

#   set {
#     name  = "controller.enableTLSPassThrough"
#     value = "true"
#   }

#   set {
#     name  = "controller.replicaCount"
#     value = 2

#   }

#   set {
#     name  = "controller.service.externalTrafficPolicy"
#     value = "Local"
#   }

#   set {
#     name  = "controller.metrics.enabled"
#     value = true
#   }

#   set {
#     name  = "controller.metrics.serviceMonitor.enabled"
#     value = true
#   }

#   set {
#     name  = "controller.metrics.serviceMonitor.additionalLabels.release"
#     value = "prometheus"
#   }

#   # Disable strict path validation, to work around a bug in ingress-nginx
#   # https://github.com/kubernetes/ingress-nginx/issues/11176
#   set {
#     name  = "strict-validate-path-type"
#     value = false
#   }

#   lifecycle {
#     ignore_changes = all
#   }

# }


# # Note:  Update NGINX Ingress config map to add
# # data:
# #  proxy-buffer-size: "8k"
# #  proxy-body-size: "25M"
