param keyVaultName string
param existingOpenAIEndpoint string
@secure()
param existingOpenAIKey string = ''
param openAIApiVersion string
param openAIModelName string

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource existingOpenAIEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-endpoint'
  properties: {
    value: existingOpenAIEndpoint
  }
}

resource existingOpenAIKeySecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-key'
  properties: {
    value: existingOpenAIKey
  }
}

resource openAIDeploymentSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-deployment'
  properties: {
    value: openAIModelName
  }
}

resource openAIVersionSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'azure-openai-version'
  properties: {
    value: openAIApiVersion
  }
}

output endpoint string = existingOpenAIEndpoint
output deploymentName string = openAIModelName
output apiVersion string = openAIApiVersion
output serviceName string = ''
