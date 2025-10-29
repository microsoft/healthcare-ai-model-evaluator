param location string
param tags object = {}
param name string
param keyVaultName string
param authClientId string
param apiBaseUrl string

resource staticWebApp 'Microsoft.Web/staticSites@2022-09-01' = {
  name: name
  location: location
  tags: union(tags, {
    'azd-service-name': 'web'
  })
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: ''
    branch: ''
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
      appLocation: '/'
      outputLocation: 'build'
      appBuildCommand: 'npm run build'
    }
  }
}

resource staticWebAppConfig 'Microsoft.Web/staticSites/config@2022-09-01' = {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    VITE_CLIENT_ID: authClientId
    VITE_API_BASE_URL: apiBaseUrl
    VITE_TENANT_ID: subscription().tenantId
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

resource webAppUrlSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'web-app-url'
  properties: {
    value: 'https://${staticWebApp.properties.defaultHostname}'
  }
}

output uri string = 'https://${staticWebApp.properties.defaultHostname}'
output name string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname 
