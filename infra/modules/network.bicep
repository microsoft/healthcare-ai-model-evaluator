param location string
param tags object = {}
param resourceToken string

@description('Create a new VNet (true) or use existing subnet IDs (false).')
param createVnet bool = true

@description('Existing VNet resource ID (informational only when using existing subnet IDs).')
param existingVnetResourceId string = ''

@description('When createVnet is false: existing subnet ID for Container Apps Environment infrastructure subnet.')
param existingAcaInfrastructureSubnetId string = ''

@description('When createVnet is false: existing subnet ID for Azure Functions VNet integration.')
param existingFunctionsIntegrationSubnetId string = ''

@description('When createVnet is true: VNet address space CIDR.')
param vnetAddressSpace string = '10.30.0.0/16'

@description('When createVnet is true: Subnet CIDR prefix for Container Apps Environment infrastructure subnet.')
param acaInfrastructureSubnetPrefix string = '10.30.0.0/23'

@description('When createVnet is true: Subnet CIDR prefix for Azure Functions VNet integration subnet.')
param functionsIntegrationSubnetPrefix string = '10.30.2.0/24'

var vnetName = 'vnet-${resourceToken}'

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = if (createVnet) {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressSpace
      ]
    }
    subnets: [
      {
        name: 'aca-infra'
        properties: {
          addressPrefix: acaInfrastructureSubnetPrefix
          delegations: [
            {
              name: 'aca-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'functions-integration'
        properties: {
          addressPrefix: functionsIntegrationSubnetPrefix
          delegations: [
            {
              name: 'functions-delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

output vnetId string = createVnet ? vnet.id : existingVnetResourceId
output acaInfrastructureSubnetId string = createVnet ? resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'aca-infra') : existingAcaInfrastructureSubnetId
output functionsIntegrationSubnetId string = createVnet ? resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'functions-integration') : existingFunctionsIntegrationSubnetId
