param location string
param tags object = {}
param containerAppsEnvName string
param containerRegistryName string
param logAnalyticsWorkspaceName string
param applicationInsightsName string
param keyVaultName string
param authClientId string
param apiImageName string
param resourceToken string
// Email and frontend settings
param webBaseUrl string = ''
param emailFrom string = 'no-reply@haime.local'
param emailSmtpHost string = ''
param emailSmtpPort int = 587
param emailSmtpUser string = ''
@secure()
param emailSmtpPass string = ''
param emailSmtpUseSsl bool = true
// Azure Communication Services (optional)
param acsKeyVaultSecretName string = ''
// Feature flag: enable/disable email communications in app
param emailEnabled bool = false

// IP filtering parameters
@description('Enable IP filtering for the web application (when true, only allowedWebIp can access the API)')
param enableWebIpFiltering bool = true

@description('IP address allowed to access the web application API (comma-delimited CIDR format, e.g., "203.0.113.1/32,198.51.100.0/24")')
param allowedWebIp string = ''

// Direct connection values to avoid Key Vault dependency during Container App creation
@description('Cosmos DB account name for connection string generation')
param cosmosAccountName string

@description('Storage account name for connection string generation')  
param storageAccountName string

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' existing = {
  name: logAnalyticsWorkspaceName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2021-09-01' existing = {
  name: containerRegistryName
}

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

// Reference existing Cosmos and Storage accounts to get connection strings directly
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosAccountName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppsEnvName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

resource apiContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'api-${resourceToken}'
  location: location
  tags: union(tags, {
    'azd-service-name': 'api'
  })
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
        }
        ipSecurityRestrictions: enableWebIpFiltering && !empty(allowedWebIp) ? [
          {
            name: 'AllowedIP'
            ipAddressRange: contains(allowedWebIp, ',') ? trim(split(allowedWebIp, ',')[0]) : allowedWebIp
            action: 'Allow'
          }
          {
            name: 'DenyAll'
            ipAddressRange: '0.0.0.0/0'
            action: 'Deny'
          }
        ] : []
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.listCredentials().username
          passwordSecretRef: 'container-registry-password'
        }
      ]
  secrets: concat([
        {
          name: 'container-registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
        {
          name: 'cosmos-connection-string'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'cosmos-endpoint'
          value: cosmosAccount.properties.documentEndpoint
        }
        {
          name: 'storage-connection-string'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'storage-endpoint'
          value: storageAccount.properties.primaryEndpoints.blob
        }
        {
          name: 'auth-client-id'
          value: authClientId
        }
      ], empty(emailSmtpPass) ? [] : [
        {
          name: 'email-smtp-password'
          value: emailSmtpPass
        }
      ], empty(acsKeyVaultSecretName) ? [] : [
        {
          name: 'acs-connection-string'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${acsKeyVaultSecretName}'
          identity: 'system'
        }
      ])
    }
    template: {
      containers: [
        {
          image: !empty(apiImageName) ? apiImageName : 'mcr.microsoft.com/dotnet/aspnet:8.0'
          name: 'api'
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: applicationInsights.properties.ConnectionString
            }
            {
              name: 'CosmosDb__ConnectionString'
              secretRef: 'cosmos-connection-string'
            }
            {
              name: 'CosmosDb__DatabaseName'
              value: 'HAIMEDB'
            }
            {
              name: 'CosmosDb__ContainerName'
              value: 'Users'
            }
            {
              name: 'COSMOSDB_CONNECTION_STRING'
              secretRef: 'cosmos-connection-string'
            }
            {
              name: 'COSMOSDB_ENDPOINT'
              secretRef: 'cosmos-endpoint'
            }
            {
              name: 'Storage__ConnectionString'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'AZURE_STORAGE_ENDPOINT'
              secretRef: 'storage-endpoint'
            }
            {
              name: 'AzureStorage__ImageContainer'
              value: 'images'
            }
            {
              name: 'AzureAd__ClientId'
              secretRef: 'auth-client-id'
            }
            {
              name: 'AzureAd__Audience'
              value: authClientId
            }
            {
              name: 'AzureAd__TenantId'
              value: subscription().tenantId
            }
            // Always include non-secret email defaults for port and SSL
            {
              name: 'Email__Smtp__Port'
              value: string(emailSmtpPort)
            }
            {
              name: 'Email__Smtp__UseSsl'
              value: string(emailSmtpUseSsl)
            }
          ],
          empty(webBaseUrl) ? [] : [
            {
              name: 'Web__BaseUrl'
              value: webBaseUrl
            }
          ],
          [
            {
              name: 'Email__Enabled'
              value: string(emailEnabled)
            }
          ],
      // Prefer ACS if provided; app code will detect this setting
      empty(acsKeyVaultSecretName) ? [] : [
            {
              name: 'Email__Acs__ConnectionString'
        secretRef: 'acs-connection-string'
            }
          ],
          empty(emailFrom) ? [] : [
            {
              name: 'Email__From'
              value: emailFrom
            }
          ],
          empty(emailSmtpHost) ? [] : [
            {
              name: 'Email__Smtp__Host'
              value: emailSmtpHost
            }
          ],
          empty(emailSmtpUser) ? [] : [
            {
              name: 'Email__Smtp__User'
              value: emailSmtpUser
            }
          ],
          empty(emailSmtpPass) ? [] : [
            {
              name: 'Email__Smtp__Pass'
              secretRef: 'email-smtp-password'
            }
          ])
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Grant Container App access to Key Vault
resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2022-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: apiContainerApp.identity.principalId
        permissions: {
          secrets: ['get', 'list', 'set', 'delete']
        }
      }
    ]
  }
}

output apiUri string = 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'
output apiName string = apiContainerApp.name
output apiPrincipalId string = apiContainerApp.identity.principalId 
