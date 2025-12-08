param location string
param tags object = {}
param accountName string
param databaseName string
param keyVaultName string
param principalId string = ''
param principalType string = 'ServicePrincipal'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'MongoDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    capabilities: [
      {
        name: 'EnableServerless'
      }
      {
        name: 'EnableMongo'
      }
    ]
    apiProperties: {
      serverVersion: '4.2'
    }
    // Use Azure service integration for security - disable public access by default
    publicNetworkAccess: 'Disabled'  // Admin scripts will enable temporarily when needed
    networkAclBypass: 'AzureServices' // Allow Azure services like Container Apps and Functions
    isVirtualNetworkFilterEnabled: false
    ipRules: []  // No specific IP restrictions - rely on managed identity and Azure service integration
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource cosmosConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'cosmos-connection-string'
  properties: {
    value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
}

// Store Cosmos DB endpoint for managed identity authentication
resource cosmosEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'cosmos-endpoint'
  properties: {
    value: cosmosAccount.properties.documentEndpoint
  }
}

// Cosmos DB Built-in Data Contributor role assignment for managed identity
resource cosmosRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  scope: cosmosAccount
  name: guid(cosmosAccount.id, principalId, 'CosmosDBAccountContributor')
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5bd9cd88-fe45-4216-938b-f97437e15450') // Cosmos DB Account Reader Writer
  }
}

output accountName string = cosmosAccount.name
output databaseName string = cosmosDatabase.name
output endpoint string = cosmosAccount.properties.documentEndpoint 
