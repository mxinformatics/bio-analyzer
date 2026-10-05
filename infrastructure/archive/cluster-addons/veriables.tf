#--- aks/dev-infrastructure/variables.tf ---#

variable "ingress_namespace" {
  type        = string
  description = "Namespace for the Nginx ingress controller"
  default     = "mxinfo-ingress"
}

variable "ingress_version" {
  type        = string
  description = "Ingress Helm chart version"
  default     = "4.14.0"
}

variable "kube_config_path" {
  type        = string
  description = "Path to the kube config file to use when deploying to the cluster"
  default     = "~/.kube/config"
}

variable "kube_config_context" {
  type        = string
  description = "Context name from the kube config file for deploying to the cluster"
  
}

variable "cert_manager_namespace" {
  type        = string
  description = "Namespace for the cert-manager resources"
  default     = "cert-manager"
}

variable "cert_manager_version" {
  type        = string
  description = "Cert manager version"
  default     = "v1.18"
}


variable "cert_manager_email" {
  type        = string
  description = "Email address for cert-manager Let's Encrypt registration"
  
}
variable "monitoring_namespace" {
  type        = string
  description = "Namespace for the monitoring resources"
  default     = "mxinfo-monitoring"
}

variable "kube_prometheus_version" {
  type        = string
  description = "Kube Prometheus version"
  default     = "75.15.0"
}


variable "prod_subscription_id" {
  type        = string
  description = "Subscription ID where the DNS Zone and the User Assigned Identity for cert-manager are located"
}
