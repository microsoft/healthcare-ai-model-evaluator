param location string
param tags object = {}
param name string
param keyVaultName string

// Parameters for Azure OpenAI configuration (new service)
param openAIApiVersion string
param openAIModelName string
param openAIModelVersion string
param modelCapacity int = 10
param modelSku string

resource openAIServiceNew 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
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


resource openAIDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
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
resource openAIEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-endpoint'
  properties: {
    value: openAIServiceNew.properties.endpoint
  }
}

// Store Azure OpenAI key in Key Vault
resource openAIKeySecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-key'
  properties: {
    value: openAIServiceNew.listKeys().key1
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
output endpoint string = openAIServiceNew.properties.endpoint
output deploymentName string = openAIModelName
output apiVersion string = openAIApiVersion
output serviceName string = openAIServiceNew.name 
