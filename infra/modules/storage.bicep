param location string
param tags object = {}
param name string
param keyVaultName string
param principalId string = ''
param principalType string = 'ServicePrincipal'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: true
    minimumTlsVersion: 'TLS1_2'
    accessTier: 'Hot'
    networkAcls: {
      defaultAction: 'Allow'  // Allow Azure services and managed identity access
      bypass: 'AzureServices'
      ipRules: []  // No specific IP restrictions - rely on managed identity
    }
  }
}

// Blob service (required parent for blob containers)
resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  parent: storageAccount
  name: 'default'
}

// Blob containers for the main application
resource medicalImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobServices
  name: 'medical-images'
  properties: {
    publicAccess: 'None'
  }
}

resource imagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobServices
  name: 'images'
  properties: {
    publicAccess: 'None'
  }
}

resource reportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobServices
  name: 'reports'
  properties: {
    publicAccess: 'None'
  }
}

// Function-specific containers for metrics processing
resource metricJobsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobServices
  name: 'metricjobs'
  properties: {
    publicAccess: 'None'
  }
}

resource metricResultsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobServices
  name: 'metricresults'
  properties: {
    publicAccess: 'None'
  }
}

resource evaluatorJobsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobServices
  name: 'evaluatorjobs'
  properties: {
    publicAccess: 'None'
  }
}

resource evaluatorResultsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobServices
  name: 'evaluatorresults'
  properties: {
    publicAccess: 'None'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'storage-connection-string'
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
  }
}

// Store Storage endpoint for managed identity authentication
resource storageEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'storage-endpoint'
  properties: {
    value: storageAccount.properties.primaryEndpoints.blob
  }
}

// Storage Blob Data Contributor role assignment for managed identity
resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  scope: storageAccount
  name: guid(storageAccount.id, principalId, 'StorageBlobDataContributor')
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
  }
}

output name string = storageAccount.name
output endpoint string = storageAccount.properties.primaryEndpoints.blob
output primaryEndpoints object = storageAccount.properties.primaryEndpoints 
