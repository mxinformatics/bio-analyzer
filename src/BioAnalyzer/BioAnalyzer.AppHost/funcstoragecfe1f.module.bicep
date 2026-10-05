@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource funcstoragecfe1f 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: take('funcstoragecfe1f${uniqueString(resourceGroup().id)}', 24)
  kind: 'StorageV2'
  location: location
  sku: {
    name: 'Standard_GRS'
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
  tags: {
    'aspire-resource-name': 'funcstoragecfe1f'
  }
}

resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  name: 'default'
  parent: funcstoragecfe1f
}

output blobEndpoint string = funcstoragecfe1f.properties.primaryEndpoints.blob

output queueEndpoint string = funcstoragecfe1f.properties.primaryEndpoints.queue

output tableEndpoint string = funcstoragecfe1f.properties.primaryEndpoints.table

output name string = funcstoragecfe1f.name