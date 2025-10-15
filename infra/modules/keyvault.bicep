param location string
param tags object = {}
param name string
param principalId string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    tenantId: subscription().tenantId
    accessPolicies: !empty(principalId) ? [
      {
        objectId: principalId
        tenantId: subscription().tenantId
        permissions: {
          keys: ['get', 'list']
          secrets: ['get', 'list', 'set', 'delete']
          certificates: ['get', 'list']
        }
      }
    ] : []
    sku: {
      family: 'A'
      name: 'standard'
    }
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

output name string = keyVault.name
output uri string = keyVault.properties.vaultUri 
