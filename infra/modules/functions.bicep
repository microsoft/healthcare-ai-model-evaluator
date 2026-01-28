param location string
param tags object = {}
param resourceToken string
param keyVaultName string
param storageAccountName string
param containerRegistryName string
param openAIEndpoint string
param openAIDeploymentName string
param openAIApiVersion string

@description('Enable regional VNet integration for the Function App (outbound).')
param enableVnetIntegration bool = false

@description('When enableVnetIntegration is true: subnet resource ID for Function App VNet integration.')
param integrationSubnetId string = ''

@description('Docker image tag')
param dockerImageTag string = 'latest'

// Reference existing resources
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = {
  name: containerRegistryName
}

// App Service Plan for function apps (Premium plan for better performance)
resource functionPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'func-plan-${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'P0v3'
    tier: 'PremiumV3'
    family: 'Pv3'
  }
  kind: 'linux'
  properties: {
    reserved: true  // Required for Linux App Service Plans
  }
}

// Main metrics processing function app
resource metricsApp 'Microsoft.Web/sites@2022-09-01' = {
  name: 'func-metrics-${resourceToken}'
  location: location
  kind: 'functionapp,linux'
  tags: union(tags, {
    'azd-service-name': 'metrics'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionPlan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerRegistry.properties.loginServer}/haime-metrics:${dockerImageTag}'
      appSettings: [
        {
          // Identity-based storage connection for triggers/bindings (no shared key / no connection string)
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccount.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'AzureWebJobsStorage__blobServiceUri'
          value: 'https://${storageAccount.name}.blob.${environment().suffixes.storage}'
        }
        {
          name: 'AzureWebJobsStorage__queueServiceUri'
          value: 'https://${storageAccount.name}.queue.${environment().suffixes.storage}'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'python'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${containerRegistry.properties.loginServer}'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_USERNAME'
          value: containerRegistry.listCredentials().username
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_PASSWORD'
          value: containerRegistry.listCredentials().passwords[0].value
        }
        {
          name: 'AZURE_OPENAI_ENDPOINT'
          value: openAIEndpoint
        }
        {
          name: 'AZURE_OPENAI_API_KEY'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=azure-openai-key)'
        }
        {
          name: 'AZURE_OPENAI_DEPLOYMENT'
          value: openAIDeploymentName
        }
        {
          name: 'AZURE_OPENAI_VERSION'
          value: openAIApiVersion
        }
      ]
      alwaysOn: true
      ftpsState: 'Disabled'
    }
    httpsOnly: true
  }
}

// Grant the metrics function app identity access to Storage data plane.
// Blob triggers use both Blob and Queue behind the scenes.
resource metricsStorageBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, metricsApp.identity.principalId, 'MetricsStorageBlobDataContributor')
  scope: storageAccount
  properties: {
    principalId: metricsApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
  }
}

resource metricsStorageQueueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, metricsApp.identity.principalId, 'MetricsStorageQueueDataContributor')
  scope: storageAccount
  properties: {
    principalId: metricsApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88') // Storage Queue Data Contributor
  }
}

// Regional VNet integration (outbound) for the metrics function app
resource metricsAppVnetConfig 'Microsoft.Web/sites/networkConfig@2022-09-01' = if (enableVnetIntegration) {
  parent: metricsApp
  name: 'virtualNetwork'
  properties: {
    subnetResourceId: integrationSubnetId
  }
}

// Key Vault access policies for metrics function app
resource functionKeyVaultAccess 'Microsoft.KeyVault/vaults/accessPolicies@2022-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: metricsApp.identity.tenantId
        objectId: metricsApp.identity.principalId
        permissions: {
          secrets: ['get']
        }
      }
    ]
  }
}

// Outputs
output metricsAppName string = metricsApp.name
output metricsAppDefaultHostName string = metricsApp.properties.defaultHostName 
output metricsAppPrincipalId string = metricsApp.identity.principalId
