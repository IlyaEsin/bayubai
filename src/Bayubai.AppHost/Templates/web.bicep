// Static Web Apps for the two SPAs; Aspire 13 has no Static Web Apps integration, so the deploy workflow uploads the files.
param location string = resourceGroup().location

resource client 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'bb-client'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

resource studio 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'bb-studio'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output clientHostname string = client.properties.defaultHostname
output studioHostname string = studio.properties.defaultHostname
