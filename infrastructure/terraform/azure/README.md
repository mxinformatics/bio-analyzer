## Terraform

### Azure/Application
- Deprectated - need to delete the remaining resources - some were manually deleted


## Troubleshooting Workload Identity

Determine if workload identity and oidc issuer are configured in the cluster:

````
export RG_NAME="<aks resource group name>"
export AKS_NAME="<aks instance name>"


az aks show \
--resource-group ${RG_NAME} \
--name ${AKS_NAME} \
--query "securityProfile.workloadIdentity.enabled" \
--output tsv


az aks show \
--resource-group ${RG_NAME} \
--name ${AKS_NAME} \
--query "oidcIssuerProfile.enabled" \
--output tsv


````

Get the OIDC Issuer URL

````
export AKS_OIDC_ISSUER="$(az aks show \
--resource-group ${RG_NAME} \
--name ${AKS_NAME} \
--query "oidcIssuerProfile.issuerUrl" \
--output tsv)"
````

Get the Managed Identity Ids

````

export USER_ASSIGNED_IDENTITY_NAME="kb-applications-poc-scus"


export USER_ASSIGNED_CLIENT_ID="$(az identity show \
--resource-group ${RG_NAME} \
--name ${USER_ASSIGNED_IDENTITY_NAME} \
--query "clientId" \
--output tsv)"

export USER_ASSIGNED_PRINCIPAL_ID="$(az identity show \
--name "${USER_ASSIGNED_IDENTITY_NAME}" \
--resource-group ${RG_NAME} \
--query "principalId" \
--output tsv)"

````

Create a service account to use

````
export SERVICE_ACCOUNT_NAME="kb-applications-workload"
export SERVICE_ACCOUNT_NAMESPACE="kb-applications"


````

k exec -it 

-- ls /var/run