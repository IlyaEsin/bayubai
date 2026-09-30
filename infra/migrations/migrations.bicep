@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param cn_outputs_azure_container_apps_environment_default_domain string

param cn_outputs_azure_container_apps_environment_id string

param migrations_containerimage string

param migrations_identity_outputs_id string

param key_vault string

param postgres_outputs_hostname string

param postgres_user_value string

@secure()
param postgres_password_value string

param insights_outputs_appinsightsconnectionstring string

param migrations_identity_outputs_clientid string

param cn_outputs_azure_container_registry_endpoint string

param cn_outputs_azure_container_registry_managed_identity_id string

resource secrets 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: key_vault
}

resource secrets_connectionstrings__carenest 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = {
  name: 'connectionstrings--carenest'
  parent: secrets
}

resource migrations 'Microsoft.App/jobs@2025-07-01' = {
  name: 'migrations'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'connectionstrings--carenest'
          identity: migrations_identity_outputs_id
          keyVaultUrl: secrets_connectionstrings__carenest.properties.secretUri
        }
        {
          name: 'carenest-uri'
          value: 'postgresql://${uriComponent(postgres_user_value)}:${uriComponent(postgres_password_value)}@${postgres_outputs_hostname}/carenest'
        }
        {
          name: 'carenest-password'
          value: postgres_password_value
        }
      ]
      triggerType: 'Manual'
      replicaTimeout: 1800
      registries: [
        {
          server: cn_outputs_azure_container_registry_endpoint
          identity: cn_outputs_azure_container_registry_managed_identity_id
        }
      ]
    }
    environmentId: cn_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: migrations_containerimage
          name: 'migrations'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ConnectionStrings__carenest'
              secretRef: 'connectionstrings--carenest'
            }
            {
              name: 'CARENEST_HOST'
              value: postgres_outputs_hostname
            }
            {
              name: 'CARENEST_PORT'
              value: '5432'
            }
            {
              name: 'CARENEST_URI'
              secretRef: 'carenest-uri'
            }
            {
              name: 'CARENEST_JDBCCONNECTIONSTRING'
              value: 'jdbc:postgresql://${postgres_outputs_hostname}/carenest?sslmode=require&authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin'
            }
            {
              name: 'CARENEST_USERNAME'
              value: postgres_user_value
            }
            {
              name: 'CARENEST_PASSWORD'
              secretRef: 'carenest-password'
            }
            {
              name: 'CARENEST_DATABASENAME'
              value: 'carenest'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: insights_outputs_appinsightsconnectionstring
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: migrations_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
            }
          ]
        }
      ]
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${migrations_identity_outputs_id}': { }
      '${cn_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}