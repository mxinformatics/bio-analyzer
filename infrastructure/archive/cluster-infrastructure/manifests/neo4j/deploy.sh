# Steps for the manual deployment of Neo4j
kubectl create namespace neo4j

# Generate the K8s manifests using Helm
helm repo add neo4j https://neo4j.github.io/helm-charts/
helm repo update
helm template neo4j neo4j/neo4j --namespace neo4j --values values.yaml > neo4j-manifests.yaml


# To Connect to cluster remotely, use port forwarding
kubectl port-forward --namespace neo4j svc/neo4j 7474:7474 7687:7687