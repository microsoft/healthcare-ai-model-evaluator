param location string
param tags object = {}
param name string
param keyVaultName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
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

output name string = storageAccount.name
output primaryEndpoints object = storageAccount.properties.primaryEndpoints 
