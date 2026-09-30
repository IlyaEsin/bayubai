@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource cn_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: take('cnacr${uniqueString(resourceGroup().id)}', 50)
  location: location
  sku: {
    name: 'Basic'
  }
  tags: {
    'aspire-resource-name': 'cn-acr'
  }
}

output name string = cn_acr.name

output loginServer string = cn_acr.properties.loginServer

output id string = cn_acr.id