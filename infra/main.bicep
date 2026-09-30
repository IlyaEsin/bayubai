targetScope = 'subscription'

param resourceGroupName string

param location string

param principalId string

param key_vault string

param postgres_user string = 'bayubai'

@secure()
param postgres_password string

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module bb_acr 'bb-acr/bb-acr.bicep' = {
  name: 'bb-acr'
  scope: rg
  params: {
    location: location
  }
}

module bb 'bb/bb.bicep' = {
  name: 'bb'
  scope: rg
  params: {
    location: location
    bb_acr_outputs_name: bb_acr.outputs.name
    userPrincipalId: principalId
  }
}

module secrets 'secrets/secrets.bicep' = {
  name: 'secrets'
  scope: rg
  params: {
    location: location
    key_vault: key_vault
  }
}

module insights 'insights/insights.bicep' = {
  name: 'insights'
  scope: rg
  params: {
    location: location
  }
}

module postgres 'postgres/postgres.bicep' = {
  name: 'postgres'
  scope: rg
  params: {
    location: location
    administratorLogin: postgres_user
    administratorLoginPassword: postgres_password
    secrets_outputs_name: secrets.outputs.name
  }
}

module web 'web/web.bicep' = {
  name: 'web'
  scope: rg
  params: {
    location: location
  }
}

module migrations_identity 'migrations-identity/migrations-identity.bicep' = {
  name: 'migrations-identity'
  scope: rg
  params: {
    location: location
  }
}

module migrations_roles_secrets 'migrations-roles-secrets/migrations-roles-secrets.bicep' = {
  name: 'migrations-roles-secrets'
  scope: rg
  params: {
    location: location
    key_vault: key_vault
    principalId: migrations_identity.outputs.principalId
  }
}

module api_identity 'api-identity/api-identity.bicep' = {
  name: 'api-identity'
  scope: rg
  params: {
    location: location
  }
}

module api_roles_secrets 'api-roles-secrets/api-roles-secrets.bicep' = {
  name: 'api-roles-secrets'
  scope: rg
  params: {
    location: location
    key_vault: key_vault
    principalId: api_identity.outputs.principalId
  }
}

output bb_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = bb.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN

output bb_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = bb.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID

output bb_AZURE_CONTAINER_REGISTRY_ENDPOINT string = bb.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT

output bb_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = bb.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID

output migrations_identity_id string = migrations_identity.outputs.id

output postgres_hostName string = postgres.outputs.hostName

output secrets_vaultUri string = secrets.outputs.vaultUri

output insights_appInsightsConnectionString string = insights.outputs.appInsightsConnectionString

output migrations_identity_clientId string = migrations_identity.outputs.clientId

output api_identity_id string = api_identity.outputs.id

output api_identity_clientId string = api_identity.outputs.clientId