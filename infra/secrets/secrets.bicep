@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param key_vault string

resource secrets 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: key_vault
}

output vaultUri string = secrets.properties.vaultUri

output name string = secrets.name

output id string = secrets.id