@description('Location for all resources')
param location string = resourceGroup().location

@description('Base name for resources')
param environmentName string = 'haime'

@description('Resource token for unique naming')
param resourceToken string = ''

@description('Key Vault name for secrets access')
param keyVaultName string

@description('Summary evaluator function app name')
param evaluatorAppName string = 'func-evaluator-${resourceToken}'

@description('Storage account name')
param storageAccountName string

@description('Azure OpenAI endpoint for summary evaluator')
param azureOpenAIEndpoint string = ''

@description('Azure OpenAI API key for summary evaluator')
@secure()
param azureOpenAIKey string = ''

@description('Azure OpenAI deployment name for summary evaluator')
param azureOpenAIDeployment string = 'gpt-4'

@description('Azure OpenAI API version for summary evaluator')
param azureOpenAIVersion string = '2024-02-01'

// Reference existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

// Reference existing storage account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName
}

// Create dedicated App Service Plan for evaluator to avoid disk space contention
resource evaluatorPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'plan-evaluator-${resourceToken}'
  location: location
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

// Summary evaluator function app with zip deployment
resource evaluatorApp 'Microsoft.Web/sites@2022-09-01' = {
  name: evaluatorAppName
  location: location
  kind: 'functionapp,linux'
  tags: {
    'azd-service-name': 'evaluator'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: evaluatorPlan.id  // Use dedicated plan instead of shared one
    siteConfig: {
      linuxFxVersion: 'Python|3.11'
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
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'true'
        }
        {
          name: 'AZURE_OPENAI_ENDPOINT'
          value: azureOpenAIEndpoint
        }
        {
          name: 'AZURE_OPENAI_API_KEY'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=azure-openai-key)'
        }
        {
          name: 'AZURE_OPENAI_DEPLOYMENT'
          value: azureOpenAIDeployment
        }
        {
          name: 'AZURE_OPENAI_VERSION'
          value: azureOpenAIVersion
        }
      ]
      alwaysOn: true
      ftpsState: 'Disabled'
    }
    httpsOnly: true
  }
}

// KeyVault access policy for evaluator function app
resource evaluatorKeyVaultAccess 'Microsoft.KeyVault/vaults/accessPolicies@2022-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: evaluatorApp.identity.tenantId
        objectId: evaluatorApp.identity.principalId
        permissions: {
          secrets: ['get']
        }
      }
    ]
  }
}

@description('Name of the summary evaluator function app')
output evaluatorAppName string = evaluatorApp.name

@description('Default hostname of the summary evaluator function app')
output evaluatorAppDefaultHostName string = evaluatorApp.properties.defaultHostName
