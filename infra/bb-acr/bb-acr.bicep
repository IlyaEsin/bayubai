@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource bb_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: take('bbacr${uniqueString(resourceGroup().id)}', 50)
  location: location
  sku: {
    name: 'Basic'
  }
  tags: {
    'aspire-resource-name': 'bb-acr'
  }
}

output name string = bb_acr.name

output loginServer string = bb_acr.properties.loginServer

output id string = bb_acr.id