@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param userPrincipalId string = ''

param tags object = { }

param cn_acr_outputs_name string

resource cn_mi 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('cn_mi-${uniqueString(resourceGroup().id)}', 128)
  location: location
  tags: tags
}

resource cn_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: cn_acr_outputs_name
}

resource cn_acr_cn_mi_AcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cn_acr.id, cn_mi.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d'))
  properties: {
    principalId: cn_mi.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalType: 'ServicePrincipal'
  }
  scope: cn_acr
}

resource cn_law 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: take('cnlaw-${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
  tags: tags
}

resource cn 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: take('cn${uniqueString(resourceGroup().id)}', 24)
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: cn_law.properties.customerId
        sharedKey: cn_law.listKeys().primarySharedKey
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

output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = cn_law.name

output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = cn_law.id

output AZURE_CONTAINER_REGISTRY_NAME string = cn_acr.name

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = cn_acr.properties.loginServer

output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = cn_mi.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = cn.name

output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = cn.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = cn.properties.defaultDomain