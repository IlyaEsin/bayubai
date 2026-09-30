@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param bb_outputs_azure_container_apps_environment_default_domain string

param bb_outputs_azure_container_apps_environment_id string

param api_containerimage string

param api_identity_outputs_id string

param api_containerport string

param key_vault string

param postgres_outputs_hostname string

param postgres_user_value string

@secure()
param postgres_password_value string

param insights_outputs_appinsightsconnectionstring string

param domain_value string

param api_identity_outputs_clientid string

param bb_outputs_azure_container_registry_endpoint string

param bb_outputs_azure_container_registry_managed_identity_id string

resource secrets 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: key_vault
}

resource secrets_connectionstrings__bayubai 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = {
  name: 'connectionstrings--bayubai'
  parent: secrets
}

resource secrets_admin_email 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = {
  name: 'admin-email'
  parent: secrets
}

resource secrets_email_username 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = {
  name: 'email-username'
  parent: secrets
}

resource secrets_email_password 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = {
  name: 'email-password'
  parent: secrets
}

resource api 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'api'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'connectionstrings--bayubai'
          identity: api_identity_outputs_id
          keyVaultUrl: secrets_connectionstrings__bayubai.properties.secretUri
        }
        {
          name: 'bayubai-uri'
          value: 'postgresql://${uriComponent(postgres_user_value)}:${uriComponent(postgres_password_value)}@${postgres_outputs_hostname}/bayubai'
        }
        {
          name: 'bayubai-password'
          value: postgres_password_value
        }
        {
          name: 'identity--adminemails--0'
          identity: api_identity_outputs_id
          keyVaultUrl: secrets_admin_email.properties.secretUri
        }
        {
          name: 'email--username'
          identity: api_identity_outputs_id
          keyVaultUrl: secrets_email_username.properties.secretUri
        }
        {
          name: 'email--password'
          identity: api_identity_outputs_id
          keyVaultUrl: secrets_email_password.properties.secretUri
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: int(api_containerport)
        transport: 'http'
      }
      registries: [
        {
          server: bb_outputs_azure_container_registry_endpoint
          identity: bb_outputs_azure_container_registry_managed_identity_id
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: bb_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          probes: [
            {
              failureThreshold: 3
              httpGet: {
                path: '/alive'
                port: int(api_containerport)
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 5
              successThreshold: 1
              timeoutSeconds: 1
              type: 'Liveness'
            }
          ]
          image: api_containerimage
          name: 'api'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'HTTP_PORTS'
              value: api_containerport
            }
            {
              name: 'ConnectionStrings__bayubai'
              secretRef: 'connectionstrings--bayubai'
            }
            {
              name: 'BAYUBAI_HOST'
              value: postgres_outputs_hostname
            }
            {
              name: 'BAYUBAI_PORT'
              value: '5432'
            }
            {
              name: 'BAYUBAI_URI'
              secretRef: 'bayubai-uri'
            }
            {
              name: 'BAYUBAI_JDBCCONNECTIONSTRING'
              value: 'jdbc:postgresql://${postgres_outputs_hostname}/bayubai?sslmode=require&authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin'
            }
            {
              name: 'BAYUBAI_USERNAME'
              value: postgres_user_value
            }
            {
              name: 'BAYUBAI_PASSWORD'
              secretRef: 'bayubai-password'
            }
            {
              name: 'BAYUBAI_DATABASENAME'
              value: 'bayubai'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: insights_outputs_appinsightsconnectionstring
            }
            {
              name: 'Frontend__Origins__0'
              value: 'https://app.${domain_value}'
            }
            {
              name: 'Frontend__Origins__1'
              value: 'https://studio.${domain_value}'
            }
            {
              name: 'Frontend__ClientAppUrl'
              value: 'https://app.${domain_value}'
            }
            {
              name: 'Identity__AdminEmails__0'
              secretRef: 'identity--adminemails--0'
            }
            {
              name: 'Email__From'
              value: 'Баюбай <no-reply@${domain_value}>'
            }
            {
              name: 'Email__Host'
              value: 'smtp-relay.brevo.com'
            }
            {
              name: 'Email__Port'
              value: '587'
            }
            {
              name: 'Email__UseStartTls'
              value: 'true'
            }
            {
              name: 'Email__UserName'
              secretRef: 'email--username'
            }
            {
              name: 'Email__Password'
              secretRef: 'email--password'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: api_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${api_identity_outputs_id}': { }
      '${bb_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}