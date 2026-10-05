# shared-infrastructure

## Argo Install Via Kustomize

```
cd argocd/manifests
kustomize build . | kubectl apply -f -
```

NOTE: The manifests are for a 3 node HA ArgoCD cluster. It requires a minimum of 4 nodes in the cluster due to node affinitiy rules.

### Change Argo Password

```
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
kubectl port-forward -n argocd svc/argocd-server 8080:80
argocd login localhost:8080
argocd account update-password
```


## Setup Argo App

```
kubectl apply -f argo/argocd-app.yaml

kubectl rollout restart -n argocd deployment argocd-repo-server

```

### Argo Backup

```
argocd admin export -n argocd > backup-$(data + "%Y-%m-%d").yaml
```

### Restore Argo Backup

````
argocd admin import -n argocd - < backup-2021-09-14.yaml
```
````

## Prometheus Operator

Do not try to install via ArgoCD and helm chart. ArgoCD alters the instance label that the alertmanager uses to identify the instance. This will cause the alertmanager to not be able to find the instance and will not be able to send alerts.

```
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update
helm install kube-prometheus-stack prometheus-community/kube-prometheus-stack -n monitoring

```

## Install ArgoCD Notifications Manually

```
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj-labs/argocd-notifications/release-1.0/manifests/install.yaml
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj-labs/argocd-notifications/release-1.0/catalog/install.yaml

```

## Test a Slack Notification

```
argocd login localhost:8080

# Trigger notification using in-cluster config map and secret
argocd admin notifications template notify custom-slack-template nginx --recipient slack:argo-status



# Render notification render generated notification in console
argocd admin notifications template notify app-sync-succeeded guestbook

```

argocd app create guestbook --repo https://github.com/DadaGore/argocd.git \
 --path guestbook --dest-namespace guestbook \
 --dest-server https://kubernetes.default.svc --directory-recurse \
 --annotations notifications.argoproj.io/subscribe.on-sync-succeeded.slack=argo-status

## Using Tokens in Commands

```
argocd proj role create-token argocd read-sync

argocd app sync <application> --auth-token <token>

```

## Ingress Install

```
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update
helm install ingress-nginx ingress-nginx/ingress-nginx -n mxinfo-ingress \
--set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz \
--set controller.service.externalTrafficPolicy=Local
```

## Manual Cert manager Install

```
helm repo add jetstack https://charts.jetstack.io
helm repo update
helm install cert-manager jetstack/cert-manager --namespace cert-manager --create-namespace --version v1.14.5 --set installCRDs=true
```

## Install External Secrets

```
helm repo add external-secrets https://charts.external-secrets.io
helm repo update

helm install external-secrets \
   external-secrets/external-secrets \
    -n external-secrets \
    --create-namespace \
   --set installCRDs=true
```

## Install Argo Rollouts

```
kubectl create namespace argo-rollouts
kubectl apply -n argo-rollouts -f https://github.com/argoproj/argo-rollouts/releases/latest/download/install.yaml
```

## Create a token to use in CI

```
argocd proj role create-token team ci-role -e 5d

```
## Kubectl plugin (or install with Homebrew)

curl -LO https://github.com/argoproj/argo-rollouts/releases/latest/download/kubectl-argo-rollouts-darwin-arm64

chmod +x ./kubectl-argo-rollouts-darwin-arm64

sudo mv ./kubectl-argo-rollouts-darwin-arm64 /usr/local/bin/kubectl-argo-rollouts

## Setup Argo Workflows

Quickstart
````
k create namespace argo
ARGO_WORKFLOWS_VERSION=v3.6.0
kubectl apply -n argo -f "https://github.com/argoproj/argo-workflows/releases/download/${ARGO_WORKFLOWS_VERSION}/quick-start-minimal.yaml"

kubectl delete -n argo -f "https://github.com/argoproj/argo-workflows/releases/download/${ARGO_WORKFLOWS_VERSION}/quick-start-minimal.yaml"

kubectl apply -n argo -f https://github.com/argoproj/argo-workflows/releases/download/v3.6.0/install.yaml

kubectl delete -n argo -f https://github.com/argoproj/argo-workflows/releases/download/v3.6.0/install.yaml

k port-forward -n argo svc/argo-server -n argo 2746:2746
````

Authentication

k create role manual-access --verb=list,update --resource=workflows.argoproj.io

k create sa manual-access

kubectl create rolebinding manual-access --role=manual-access --serviceaccount=argo:manual-access

kubectl apply -f - <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: manual-token
  namespace: argo
  annotations:
    kubernetes.io/service-account.name: manual-access
type: kubernetes.io/service-account-token
EOF


ARGO_TOKEN="Bearer $(kubectl get secret -n argo manual-token -o=jsonpath='{.data.token}' | base64 --decode)"
echo $ARGO_TOKEN


kubectl apply -f - <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: wf-token
  namespace: argo
  annotations:
    kubernetes.io/service-account.name: argo-workflow-sa
type: kubernetes.io/service-account-token
EOF

ARGO_TOKEN="Bearer $(kubectl get secret -n argo wf-token -o=jsonpath='{.data.token}' | base64 --decode)"
echo $ARGO_TOKEN




## Missing Permission on VNET
f4d4320e-af53-4587-951c-c80711bd7a9a

az role assignment create --assignee <Client_Object_ID_or_SPN> --role "Contributor" --scope <Resource_Id>

export SP_ID="bf1c2523-3163-41d3-b230-2b0192c2be49"
az role assignment create --assignee $SP_ID --role "Contributor" --scope "/subscriptions/6b007381-ecf8-41b1-9256-3c6aa8a81d37/resourceGroups/rg-verida-knowledge-apps-prod-eus/providers/Microsoft.Network/virtualNetworks/vnet-verida-knowledge-apps-prod-eus/subnets/snet-verida-knowledge-apps-ver01-prod-eus"

az role assignment create --assignee $SP_ID --role "Contributor" --scope "/subscriptions/6b007381-ecf8-41b1-9256-3c6aa8a81d37/resourceGroups/rg-verida-knowledge-apps-prod-eus/providers/Microsoft.Network/virtualNetworks/vnet-verida-knowledge-apps-prod-eus/subnets/snet-verida-knowledge-apps-ver02-prod-eus"

az role assignment create --assignee $SP_ID --role "Contributor" --scope "/subscriptions/6b007381-ecf8-41b1-9256-3c6aa8a81d37/resourceGroups/mc_rg-verida-knowledge-apps-prod-eus_aks-verida-knowledge-apps-prod-eus_eastus/providers/Microsoft.Compute/virtualMachineScaleSets/aks-ver01-24191798-vmss"

az role assignment create --assignee $SP_ID --role "Contributor" --scope "/subscriptions/6b007381-ecf8-41b1-9256-3c6aa8a81d37/resourceGroups/mc_rg-verida-knowledge-apps-prod-eus_aks-verida-knowledge-apps-prod-eus_eastus/providers/Microsoft.Compute/virtualMachineScaleSets/aks-ver02-26248839-vmss"

az role assignment create --assignee $SP_ID --role "Network Contributor" --scope "/subscriptions/6b007381-ecf8-41b1-9256-3c6aa8a81d37/resourceGroups/rg-verida-knowledge-apps-prod-eus/providers/Microsoft.Network/virtualNetworks/vnet-verida-knowledge-apps-prod-eus/subnets/snet-verida-knowledge-apps-ver01-prod-eus"

az role assignment create --assignee $SP_ID --role "Network Contributor" --scope "/subscriptions/6b007381-ecf8-41b1-9256-3c6aa8a81d37/resourceGroups/rg-verida-knowledge-apps-prod-eus/providers/Microsoft.Network/virtualNetworks/vnet-verida-knowledge-apps-prod-eus/subnets/snet-verida-knowledge-apps-ver02-prod-eus"