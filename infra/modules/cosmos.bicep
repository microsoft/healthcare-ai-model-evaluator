param location string
param tags object = {}
param accountName string
param databaseName string
param keyVaultName string
param principalId string = ''
param principalType string = 'ServicePrincipal'
<<<<<<< HEAD

@description('List of SQL API container names to provision')
param containerNames array = [
  'Users'
  'Models'
  'Experiments'
  'ClinicalTasks'
  'TestScenarios'
  'DataObjects'
  'DataSets'
  'Images'
  'Trials'
]

@description('Partition key path used for all containers')
param partitionKeyPath string = '/id'
=======
>>>>>>> origin/main

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: accountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
<<<<<<< HEAD
    // Key-based auth MUST be disabled. The app uses Microsoft Entra ID / Managed Identity.
=======
>>>>>>> origin/main
    disableLocalAuth: true
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
    ]
<<<<<<< HEAD
    // Allow public access (network mode may further restrict this in private deployments)
    publicNetworkAccess: 'Enabled'  
    networkAclBypass: 'AzureServices' // Allow Azure services like Container Apps and Functions
    isVirtualNetworkFilterEnabled: false
    ipRules: []
=======
    apiProperties: {
      serverVersion: '4.2'
    }
    // Allow public access but secure with managed identity authentication
    publicNetworkAccess: 'Enabled'  
    networkAclBypass: 'AzureServices' // Allow Azure services like Container Apps and Functions
    isVirtualNetworkFilterEnabled: false
    ipRules: []  // No specific IP restrictions - rely on managed identity authentication
>>>>>>> origin/main
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

resource cosmosContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = [for containerName in containerNames: {
  parent: cosmosDatabase
  name: containerName
  properties: {
    resource: {
      id: containerName
      partitionKey: {
        paths: [
          partitionKeyPath
        ]
        kind: 'Hash'
      }
      // Keep indexing enabled for query flexibility (filters, projections, counts).
      // This is close to the Cosmos defaults, but explicit so fresh installs are consistent.
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
    }
  }
}]

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

// Store Cosmos DB endpoint for managed identity authentication
resource cosmosEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'cosmos-endpoint'
  properties: {
    value: cosmosAccount.properties.documentEndpoint
  }
}

// Cosmos DB SQL API data-plane RBAC role assignment for managed identity.
// Built-in role definition IDs:
// - Data Reader:      00000000-0000-0000-0000-000000000001
// - Data Contributor: 00000000-0000-0000-0000-000000000002
resource cosmosSqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = if (!empty(principalId)) {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, principalId, 'sql-data-contributor')
  properties: {
    principalId: principalId
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: '/'
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

output containerNames array = containerNames
