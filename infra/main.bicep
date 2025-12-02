targetScope = 'resourceGroup'

// This is kept in sync with azure.yaml metadata.template, and it's used for telemetry and traceability.
var azdTemplateId = 'healthcare-ai-model-evaluator@0.0.1'
var projectName = 'healthcare-ai-model-evaluator'

@minLength(1)
@maxLength(64)
@description('Name of the environment used to generate a short unique hash for resources.')
param environmentName string

@description('Primary location for all resources')
param location string = resourceGroup().location

@description('Location to deploy Key Vault')
param keyVaultLocation string = resourceGroup().location

@description('Location to deploy Cosmos DB')
param cosmosLocation string = resourceGroup().location

@description('Location to deploy Container Registry')
param containerRegistryLocation string = resourceGroup().location

@description('Location to deploy Storage Account')
param storageLocation string = resourceGroup().location

@description('Location to deploy Azure Functions')
param functionsLocation string = resourceGroup().location

@description('Id of the user or app to assign application roles')
param principalId string = deployer().objectId

@description('The image name for the API service')
param apiImageName string = ''

// Resource name overrides (optional - automatically generated if left blank)
@description('Name of the OpenAI service. Automatically generated if left blank')
param openAIServiceName string = ''

@description('Name of the Cosmos DB account. Automatically generated if left blank')
param cosmosAccountName string = ''

@description('Name of the Container Registry. Automatically generated if left blank')
param containerRegistryName string = ''

@description('Name of the Storage Account. Automatically generated if left blank')
param storageAccountName string = ''

@description('Name of the Key Vault. Automatically generated if left blank')
param keyVaultName string = ''

@description('Name of the Container Apps Environment. Automatically generated if left blank')
param containerAppsEnvName string = ''

@description('Name of the Log Analytics Workspace. Automatically generated if left blank')
param logAnalyticsName string = ''

@description('Name of the Application Insights. Automatically generated if left blank')
param applicationInsightsName string = ''

// Azure OpenAI configuration parameters
@description('Location to deploy AI Services')
param gptDeploymentLocation string = resourceGroup().location

@description('Whether to create a new Azure OpenAI service or use existing')
param createOpenAI bool = true

@description('Existing Azure OpenAI endpoint (if not creating new)')
param existingOpenAIEndpoint string = ''

@description('Existing Azure OpenAI API key (if not creating new)')
@secure()
param existingOpenAIKey string = ''

@description('Azure OpenAI API version')
param openAIApiVersion string = '2025-01-01-preview'

@description('Azure OpenAI model name and version to deploy. See: https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/reasoning')
@allowed(['o3-mini;2025-01-31', 'o4-mini;2025-04-16', 'gpt-4o;2024-08-06','gpt-4.1;2025-04-14','gpt-5;2025-08-07','gpt-5-mini;2025-08-07','gpt-5-nano;2025-08-07'])
param model string

var openAIModelName = split(model, ';')[0]
var openAIModelVersion = split(model, ';')[1]

@description('Tokens per minute capacity for the model. Units of 1000 (capacity = 100 means 100K tokens per minute)')
param modelCapacity int
// https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/deployment-types
@description('Specify the deployment type of the model. "Standard" & "DataZoneStandard" only allow data processing and data storage within the specified Azure geography. GPT5 only supports "GlobalStandard" as of now. Please be aware that this can lead to data storage and data processing outside of your azure region!')
@allowed(['DataZoneStandard', 'Standard','GlobalStandard'])
param modelSku string

@description('Enable Summary Evaluator Addon deployment (deployed as an extra Azure Function app)')
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

@description('Whether to prompt for admin user creation during first deployment')
param createAdminUser bool = true

@description('Tags for all AI resources created. JSON object')
param tagParam object = {}

// Tags that should be applied to all resources
var tags = union(tagParam, {
  'azd-env-name': environmentName
  'azd-template-id': azdTemplateId
})

// Define templated deployment name for traceability
var deploymentName = '${projectName}-${uniqueString(deployment().name)}'

// Generate a unique token to be used in naming resources
var uniqueSuffix = substring(uniqueString(subscription().id, resourceGroup().name, environmentName), 1, 3)

// Centralized resource naming with override capability
var names = {
  openai: !empty(openAIServiceName) ? openAIServiceName : 'openai-${uniqueSuffix}'
  cosmos: !empty(cosmosAccountName) ? cosmosAccountName : 'cosmos-${uniqueSuffix}'
  registry: !empty(containerRegistryName) ? containerRegistryName : 'cr${uniqueSuffix}'
  storage: !empty(storageAccountName) ? storageAccountName : 'st${uniqueSuffix}'
  keyVault: !empty(keyVaultName) ? keyVaultName : 'kv-${uniqueSuffix}'
  containerAppsEnv: !empty(containerAppsEnvName) ? containerAppsEnvName : 'cae-${uniqueSuffix}'
  logAnalytics: !empty(logAnalyticsName) ? logAnalyticsName : 'log-${uniqueSuffix}'
  appInsights: !empty(applicationInsightsName) ? applicationInsightsName : 'appi-${uniqueSuffix}'
}

// Core infrastructure first
module monitoring './modules/monitoring.bicep' = {
  name: '${deploymentName}-monitoring'
  params: {
    location: location
    tags: tags
    logAnalyticsName: names.logAnalytics
    applicationInsightsName: names.appInsights
  }
}

module registry './modules/registry.bicep' = {
  name: '${deploymentName}-registry'
  params: {
    location: empty(containerRegistryLocation) ? location : containerRegistryLocation
    tags: tags
    name: names.registry
  }
}

module keyVault './modules/keyvault.bicep' = {
  name: '${deploymentName}-keyvault'
  params: {
    location: empty(keyVaultLocation) ? location : keyVaultLocation
    tags: tags
    name: names.keyVault
    principalId: principalId
  }
}

// Azure OpenAI service (depends on key vault for secret storage)
module openAI './modules/openai.bicep' = {
  name: '${deploymentName}-openai'
  params: {
    location: empty(gptDeploymentLocation) ? location : gptDeploymentLocation
    tags: tags
    name: names.openai
    keyVaultName: keyVault.outputs.name
    createOpenAI: createOpenAI
    existingOpenAIEndpoint: existingOpenAIEndpoint
    existingOpenAIKey: existingOpenAIKey
    openAIApiVersion: openAIApiVersion
    openAIModelName: openAIModelName
    openAIModelVersion: openAIModelVersion
    modelCapacity: modelCapacity
    modelSku: modelSku
  }
}

// Data services (depends on key vault)
module cosmos './modules/cosmos.bicep' = {
  name: '${deploymentName}-cosmos'
  params: {
    location: empty(cosmosLocation) ? location : cosmosLocation
    tags: tags
    accountName: names.cosmos
    databaseName: 'HAIMEDB'
    keyVaultName: keyVault.outputs.name
  }
}

module storage './modules/storage.bicep' = {
  name: '${deploymentName}-storage'
  params: {
    location: empty(storageLocation) ? location : storageLocation
    tags: tags
    name: names.storage
    keyVaultName: keyVault.outputs.name
  }
}

// Auth placeholder (simple, minimal dependencies)
module auth './modules/auth.bicep' = {
  name: '${deploymentName}-auth'
  params: {
    location: location
    tags: tags
    name: 'auth-${uniqueSuffix}'
    keyVaultName: keyVault.outputs.name
  }
}

// Azure Functions for metrics processing
module functions './modules/functions.bicep' = {
  name: '${deploymentName}-functions'
  params: {
    location: empty(functionsLocation) ? location : functionsLocation
    tags: tags
    resourceToken: uniqueSuffix
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
  name: '${deploymentName}-acs'
  params: {
    tags: tags
    name: 'acs-${uniqueSuffix}'
    keyVaultName: keyVault.outputs.name
    // You can override this via params if needed
    dataLocation: 'United States'
  }
}


// Resolve ACS connection string (empty when disabled). Note: access output only when module is instantiated via ternary inline at usage sites.

// Application services (depends on all infrastructure)
module containerApps './modules/containerapps.bicep' = {
  name: '${deploymentName}-containerapps'
  params: {
    location: location
    tags: tags
    containerAppsEnvName: names.containerAppsEnv
    containerRegistryName: registry.outputs.name
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    keyVaultName: keyVault.outputs.name
    authClientId: auth.outputs.clientId
    apiImageName: apiImageName
    resourceToken: uniqueSuffix
  // new params for web/email config
  webBaseUrl: webBaseUrl
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
  name: '${deploymentName}-evaluator-addon'
  params: {
    location: location
    resourceToken: uniqueSuffix
    storageAccountName: storage.outputs.name
    keyVaultName: keyVault.outputs.name
    azureOpenAIEndpoint: openAI.outputs.endpoint
    azureOpenAIDeployment: openAI.outputs.deploymentName
    azureOpenAIVersion: openAI.outputs.apiVersion
  }
  dependsOn: [
    functions
  ]
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
output WEB_BASE_URL string = '${containerApps.outputs.apiUri}/webapp'

// Function app outputs
output METRICS_FUNCTION_APP_NAME string = functions.outputs.metricsAppName
output METRICS_FUNCTION_APP_URL string = 'https://${functions.outputs.metricsAppDefaultHostName}'
output EVALUATOR_FUNCTION_APP_NAME string = enableEvaluatorAddon ? evaluatorAddon.outputs.evaluatorAppName : ''
output EVALUATOR_FUNCTION_APP_URL string = enableEvaluatorAddon ? 'https://${evaluatorAddon.outputs.evaluatorAppDefaultHostName}' : ''

// Azure OpenAI outputs
output AZURE_OPENAI_ENDPOINT string = openAI.outputs.endpoint
output AZURE_OPENAI_DEPLOYMENT string = openAI.outputs.deploymentName
output AZURE_OPENAI_SERVICE_NAME string = openAI.outputs.serviceName 
