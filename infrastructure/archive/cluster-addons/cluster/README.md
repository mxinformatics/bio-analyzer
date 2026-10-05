# Knowledge Apps Infrastructure

## Pre-requisites

The use of Container Apps requires the feature to be registered in the destination subscription.

Check the status
````
az provider list --query "[].{Provider:namespace, Status:registrationState}" --out table | grep Microsft.App


Register if NotRegistered
````
az provider register --namespace Microsoft.App


````