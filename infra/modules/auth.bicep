param location string
param tags object = {}
param name string
param keyVaultName string

// This module will be used to store auth configuration
// The actual app registration will be created by the post-provision script
// since Azure AD app registration is not directly supported in Bicep templates

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

// Placeholder secret that will be updated by post-provision script
resource clientIdSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'auth-client-id'
  properties: {
    value: 'placeholder-will-be-updated-by-script'
  }
}

resource tenantIdSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'auth-tenant-id'
  properties: {
    value: subscription().tenantId
  }
}

// Output the placeholder value that will be replaced by post-provision script
output clientId string = 'placeholder-will-be-updated-by-script'
output tenantId string = subscription().tenantId 
