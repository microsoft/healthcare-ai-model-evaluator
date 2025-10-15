param location string
param tags object = {}
param resourceToken string
param keyVaultName string
param storageAccountName string
param containerRegistryName string
param openAIEndpoint string
param openAIDeploymentName string
param openAIApiVersion string

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
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
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
