@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param userPrincipalId string = ''

param tags object = { }

param bb_acr_outputs_name string

resource bb_mi 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('bb_mi-${uniqueString(resourceGroup().id)}', 128)
  location: location
  tags: tags
}

resource bb_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: bb_acr_outputs_name
}

resource bb_acr_bb_mi_AcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(bb_acr.id, bb_mi.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d'))
  properties: {
    principalId: bb_mi.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalType: 'ServicePrincipal'
  }
  scope: bb_acr
}

resource bb_law 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: take('bblaw-${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
  tags: tags
}

resource bb 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: take('bb${uniqueString(resourceGroup().id)}', 24)
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: bb_law.properties.customerId
        sharedKey: bb_law.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
  tags: tags
}

output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = bb_law.name

output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = bb_law.id

output AZURE_CONTAINER_REGISTRY_NAME string = bb_acr.name

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = bb_acr.properties.loginServer

output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = bb_mi.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = bb.name

output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = bb.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = bb.properties.defaultDomain