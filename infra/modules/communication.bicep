param tags object = {}
param name string
param keyVaultName string
@description('Where ACS stores data (e.g., United States, Europe)')
param dataLocation string = 'United States'

// Existing Key Vault to store the ACS connection string
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

// Azure Communication Services resource
resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: name
  // ACS resources are created in the global region regardless of deployment location
  location: 'global'
  tags: tags
  properties: {
    // Controls where ACS stores data
    dataLocation: dataLocation
  }
}

// Store ACS connection string in Key Vault
resource acsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'acs-connection-string'
  properties: {
    value: communicationService.listKeys().primaryConnectionString
  }
}

output name string = communicationService.name
output connectionStringSecretName string = acsConnectionStringSecret.name
