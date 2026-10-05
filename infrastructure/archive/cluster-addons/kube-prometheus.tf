# resource "kubernetes_namespace" "prom_stack" {
#   metadata {
#     name = var.monitoring_namespace
#     labels = {
#       istio-injection = "disabled"
#     }
#   }
# }

# resource "helm_release" "kube_prometheus" {
#   name       = "kube-prometheus-stack"
#   repository = "https://prometheus-community.github.io/helm-charts"
#   chart      = "prometheus-community/kube-prometheus-stack"
#   namespace  = var.monitoring_namespace
#   version    = var.kube_prometheus_version
#   set {
#     name  = "prometheus.prometheusSpec.serviceMonitorSelector.matchLabels.release"
#     value = "kube-prometheus-stack"
#   }
# }

#     helm repo add prometheus-community https://prometheus-community.github.io/helm-charts

# helm install kube-prometheus-stack prometheus-community/kube-prometheus-stack \
#    --namespace mxinfo-monitoring

#   Get Grafana 'admin' user password by running:

#   kubectl --namespace mxinfo-monitoring get secrets kube-prometheus-stack-grafana -o jsonpath="{.data.admin-password}" | base64 -d ; echo
# Access Grafana local instance:

#   export POD_NAME=$(kubectl --namespace mxinfo-monitoring get pod -l "app.kubernetes.io/name=grafana,app.kubernetes.io/instance=kube-prometheus-stack" -oname)
#   kubectl --namespace mxinfo-monitoring port-forward $POD_NAME 3000