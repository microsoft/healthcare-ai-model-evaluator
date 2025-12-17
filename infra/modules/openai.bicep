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

// When createOpenAI=true, create a new Azure OpenAI account.
resource openAIServiceNew 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (createOpenAI) {
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


// Avoid dereferencing conditional resources by creating secrets conditionally.

// Create deployment for the model if creating new service
resource openAIDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = if (createOpenAI) {
  parent: openAIServiceNew
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
resource openAIEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = if (createOpenAI) {
  parent: keyVault
  name: 'azure-openai-endpoint'
  properties: {
    value: openAIServiceNew.properties.endpoint
  }
}

resource existingOpenAIEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = if (!createOpenAI) {
  parent: keyVault
  name: 'azure-openai-endpoint'
  properties: {
    value: existingOpenAIEndpoint
  }
}

// Store Azure OpenAI key in Key Vault
resource openAIKeySecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = if (createOpenAI) {
  parent: keyVault
  name: 'azure-openai-key'
  properties: {
    value: openAIServiceNew.listKeys().key1
  }
}

resource existingOpenAIKeySecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = if (!createOpenAI) {
  parent: keyVault
  name: 'azure-openai-key'
  properties: {
    value: existingOpenAIKey
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
output endpoint string = createOpenAI ? openAIServiceNew.properties.endpoint : existingOpenAIEndpoint
output deploymentName string = openAIModelName
output apiVersion string = openAIApiVersion
output serviceName string = createOpenAI ? openAIServiceNew.name : '' 
