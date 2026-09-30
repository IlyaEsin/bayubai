@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param bb_outputs_azure_container_apps_environment_default_domain string

param bb_outputs_azure_container_apps_environment_id string

param migrations_containerimage string

param migrations_identity_outputs_id string

param postgres_outputs_hostname string

param postgres_user_value string

@secure()
param postgres_password_value string

@secure()
param postgres_app_password_value string

param insights_outputs_appinsightsconnectionstring string

param migrations_identity_outputs_clientid string

param bb_outputs_azure_container_registry_endpoint string

param bb_outputs_azure_container_registry_managed_identity_id string

resource migrations 'Microsoft.App/jobs@2025-07-01' = {
  name: 'migrations'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'connectionstrings--bayubai'
          value: 'Host=${postgres_outputs_hostname};Database=bayubai;Username=${postgres_user_value};Password=${postgres_password_value};SSL Mode=VerifyFull'
        }
        {
          name: 'database--approlepassword'
          value: postgres_app_password_value
        }
      ]
      triggerType: 'Manual'
      replicaTimeout: 1800
      registries: [
        {
          server: bb_outputs_azure_container_registry_endpoint
          identity: bb_outputs_azure_container_registry_managed_identity_id
        }
      ]
    }
    environmentId: bb_outputs_azure_container_apps_environment_id
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
              name: 'ConnectionStrings__bayubai'
              secretRef: 'connectionstrings--bayubai'
            }
            {
              name: 'Database__AppRole'
              value: 'bayubai_app'
            }
            {
              name: 'Database__AppRolePassword'
              secretRef: 'database--approlepassword'
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
      '${bb_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}