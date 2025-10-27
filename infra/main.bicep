@minLength(1)
@maxLength(64)
@description('Name of the environment used to generate a short unique hash for resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = deployer().objectId

@description('The image name for the API service')
param apiImageName string = ''

// Azure OpenAI configuration parameters
@description('Whether to create a new Azure OpenAI service or use existing')
param createOpenAI bool = true

@description('Existing Azure OpenAI endpoint (if not creating new)')
param existingOpenAIEndpoint string = ''

@description('Existing Azure OpenAI API key (if not creating new)')
@secure()
param existingOpenAIKey string = ''

@description('Azure OpenAI deployment name')
param openAIDeploymentName string = 'o3-mini'

@description('Azure OpenAI API version')
param openAIApiVersion string = '2025-01-01-preview'

@description('Azure OpenAI model configuration')
param openAIModelName string = 'o3-mini'
param openAIModelVersion string = ''

@description('Enable Summary Evaluator addon deployment')
param enableEvaluatorAddon bool = true

@description('Docker image tag for functions')
param dockerImageTag string = 'latest'

// Email via Azure Communication Services
@description('Whether to provision Azure Communication Services for email (requires Microsoft.Communication provider registration)')
param enableAcs bool = true
@description('Whether email communications features in the app are enabled (hides UI and disables sending when false)')
param emailEnabled bool = false

// Frontend and email configuration (can be overridden via azd env)
@description('Override the web base URL used by API to build links; defaults to deployed Static Web App URL')
param webBaseUrl string = ''

@description('Default From address for outbound emails')
param emailFrom string = 'no-reply@haime.local'

@description('SMTP host for outbound email (optional)')
param emailSmtpHost string = ''

@description('SMTP port for outbound email')
param emailSmtpPort int = 587

@description('SMTP username (optional)')
param emailSmtpUser string = ''

@description('SMTP password (optional)')
@secure()
param emailSmtpPass string = ''

@description('SMTP SSL/TLS usage')
param emailSmtpUseSsl bool = true


// Generate a unique token to be used in naming resources
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// Tags that should be applied to all resources
var tags = {
  'azd-env-name': environmentName
}

// Core infrastructure first
module monitoring './modules/monitoring.bicep' = {
  name: '${deployment().name}-monitoring'
  params: {
    location: location
    tags: tags
    logAnalyticsName: 'log-${resourceToken}'
    applicationInsightsName: 'appi-${resourceToken}'
  }
}

module registry './modules/registry.bicep' = {
  name: '${deployment().name}-registry'
  params: {
    location: location
    tags: tags
    name: 'cr${resourceToken}'
  }
}

module keyVault './modules/keyvault.bicep' = {
  name: '${deployment().name}-keyvault'
  params: {
    location: location
    tags: tags
    name: 'kv-${resourceToken}'
    principalId: principalId
  }
}

// Azure OpenAI service (depends on key vault for secret storage)
module openAI './modules/openai.bicep' = {
  name: '${deployment().name}-openai'
  params: {
    location: location
    tags: tags
    name: 'openai-${resourceToken}'
    keyVaultName: keyVault.outputs.name
    createOpenAI: createOpenAI
    existingOpenAIEndpoint: existingOpenAIEndpoint
    existingOpenAIKey: existingOpenAIKey
    openAIDeploymentName: openAIDeploymentName
    openAIApiVersion: openAIApiVersion
    openAIModelName: openAIModelName
    openAIModelVersion: openAIModelVersion
  }
}

// Data services (depends on key vault)
module cosmos './modules/cosmos.bicep' = {
  name: '${deployment().name}-cosmos'
  params: {
    location: location
    tags: tags
    accountName: 'cosmos-${resourceToken}'
    databaseName: 'HAIMEDB'
    keyVaultName: keyVault.outputs.name
  }
}

module storage './modules/storage.bicep' = {
  name: '${deployment().name}-storage'
  params: {
    location: location
    tags: tags
    name: 'st${resourceToken}'
    keyVaultName: keyVault.outputs.name
  }
}

// Auth placeholder (simple, minimal dependencies)
module auth './modules/auth.bicep' = {
  name: '${deployment().name}-auth'
  params: {
    location: location
    tags: tags
    name: 'auth-${resourceToken}'
    keyVaultName: keyVault.outputs.name
  }
}

// Azure Functions for metrics processing (depends on storage, registry, openai)
module functions './modules/functions.bicep' = {
  name: '${deployment().name}-functions'
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    keyVaultName: keyVault.outputs.name
    storageAccountName: storage.outputs.name
    containerRegistryName: registry.outputs.name
    openAIEndpoint: openAI.outputs.endpoint
    openAIDeploymentName: openAI.outputs.deploymentName
    openAIApiVersion: openAI.outputs.apiVersion
    dockerImageTag: dockerImageTag
  }
}

// Azure Communication Services (optional)
module acs './modules/communication.bicep' = if (enableAcs) {
  name: '${deployment().name}-acs'
  params: {
    tags: tags
    name: 'acs-${resourceToken}'
    keyVaultName: keyVault.outputs.name
    // You can override this via params if needed
    dataLocation: 'United States'
  }
}


// Resolve ACS connection string (empty when disabled). Note: access output only when module is instantiated via ternary inline at usage sites.

// Application services (depends on all infrastructure)
module containerApps './modules/containerapps.bicep' = {
  name: '${deployment().name}-containerapps'
  params: {
    location: location
    tags: tags
    containerAppsEnvName: 'cae-${resourceToken}'
    containerRegistryName: registry.outputs.name
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    keyVaultName: keyVault.outputs.name
    authClientId: auth.outputs.clientId
    apiImageName: apiImageName
    resourceToken: resourceToken
  // new params for web/email config
  webBaseUrl: empty(webBaseUrl) ? staticWebApp.outputs.uri : webBaseUrl
  emailFrom: emailFrom
  emailSmtpHost: emailSmtpHost
  emailSmtpPort: emailSmtpPort
  emailSmtpUser: emailSmtpUser
  emailSmtpPass: emailSmtpPass
  emailSmtpUseSsl: emailSmtpUseSsl
  // Azure Communication Services
  acsKeyVaultSecretName: enableAcs ? 'acs-connection-string' : ''
  emailEnabled: emailEnabled
  }
}

// Conditional evaluator addon deployment
module evaluatorAddon './modules/addons/evaluator.bicep' = if (enableEvaluatorAddon) {
  name: '${deployment().name}-evaluator-addon'
  params: {
    location: location
    resourceToken: resourceToken
    storageAccountName: storage.outputs.name
    keyVaultName: keyVault.outputs.name
    azureOpenAIEndpoint: openAI.outputs.endpoint
    azureOpenAIDeployment: openAI.outputs.deploymentName
    azureOpenAIVersion: openAI.outputs.apiVersion
  }
  dependsOn: [
    keyVault
    storage
    openAI
    functions
  ]
}

module staticWebApp './modules/staticwebapp.bicep' = {
  name: '${deployment().name}-staticwebapp'
  params: {
    location: location
    tags: tags
    name: 'swa-${resourceToken}'
    keyVaultName: keyVault.outputs.name
  authClientId: auth.outputs.clientId
  apiBaseUrl: ''
  }
}

// App outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP_NAME string = resourceGroup().name
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = registry.outputs.name
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output COSMOS_ACCOUNT_NAME string = cosmos.outputs.accountName
output STORAGE_ACCOUNT_NAME string = storage.outputs.name
output AUTH_CLIENT_ID string = auth.outputs.clientId
output API_BASE_URL string = containerApps.outputs.apiUri
output WEB_BASE_URL string = staticWebApp.outputs.uri

// Function app outputs
output METRICS_FUNCTION_APP_NAME string = functions.outputs.metricsAppName
output METRICS_FUNCTION_APP_URL string = 'https://${functions.outputs.metricsAppDefaultHostName}'
output EVALUATOR_FUNCTION_APP_NAME string = enableEvaluatorAddon ? evaluatorAddon.outputs.evaluatorAppName : ''
output EVALUATOR_FUNCTION_APP_URL string = enableEvaluatorAddon ? 'https://${evaluatorAddon.outputs.evaluatorAppDefaultHostName}' : ''

// Azure OpenAI outputs
output AZURE_OPENAI_ENDPOINT string = openAI.outputs.endpoint
output AZURE_OPENAI_DEPLOYMENT string = openAI.outputs.deploymentName
output AZURE_OPENAI_SERVICE_NAME string = openAI.outputs.serviceName 
