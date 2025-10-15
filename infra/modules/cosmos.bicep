param location string
param tags object = {}
param accountName string
param databaseName string
param keyVaultName string

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

output accountName string = cosmosAccount.name
output databaseName string = cosmosDatabase.name
output endpoint string = cosmosAccount.properties.documentEndpoint 
