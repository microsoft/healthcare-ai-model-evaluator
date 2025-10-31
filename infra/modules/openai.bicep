param location string
param tags object = {}
param name string
param keyVaultName string

// Parameters for Azure OpenAI configuration
param createOpenAI bool
param existingOpenAIEndpoint string
@secure()
param existingOpenAIKey string = ''
param openAIApiVersion string
param openAIModelName string
param openAIModelVersion string
param modelCapacity int = 10
param modelSku string

// Create Azure OpenAI service if requested
resource openAIService 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (createOpenAI) {
  name: name
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    networkAcls: {
      defaultAction: 'Allow'
    }
    publicNetworkAccess: 'Enabled'
  }
}

// Create deployment for the model if creating new service
resource openAIDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = if (createOpenAI) {
  parent: openAIService
  name: openAIModelName
  properties: {
    model: {
      format: 'OpenAI'
      name: openAIModelName
      version: !empty(openAIModelVersion) ? openAIModelVersion : null
    }
    raiPolicyName: 'Microsoft.Default'
  }
  sku: {
    capacity: modelCapacity
    name: modelSku
  }
}

// Reference to existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

// Store Azure OpenAI endpoint in Key Vault
resource openAIEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-endpoint'
  properties: {
    value: createOpenAI ? openAIService.properties.endpoint : existingOpenAIEndpoint
  }
}

// Store Azure OpenAI key in Key Vault
resource openAIKeySecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-key'
  properties: {
    value: createOpenAI ? openAIService.listKeys().key1 : existingOpenAIKey
  }
}

// Store deployment name in Key Vault
resource openAIDeploymentSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-deployment'
  properties: {
    value: openAIModelName
  }
}

// Store API version in Key Vault
resource openAIVersionSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-version'
  properties: {
    value: openAIApiVersion
  }
}

// Outputs
output endpoint string = createOpenAI ? openAIService.properties.endpoint : existingOpenAIEndpoint
output deploymentName string = openAIModelName
output apiVersion string = openAIApiVersion
output serviceName string = createOpenAI ? openAIService.name : '' 
