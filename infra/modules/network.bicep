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

@description('When createVnet is false and createPrivateEndpoint is true: existing subnet ID for private endpoints.')
param existingPrivateEndpointSubnetId string = ''

@description('When createVnet is true: VNet address space CIDR.')
param vnetAddressSpace string = '10.30.0.0/16'

@description('When createVnet is true: Subnet CIDR prefix for Container Apps Environment infrastructure subnet.')
param acaInfrastructureSubnetPrefix string = '10.30.0.0/23'

@description('When createVnet is true: Subnet CIDR prefix for Azure Functions VNet integration subnet.')
param functionsIntegrationSubnetPrefix string = '10.30.2.0/24'

@description('When createVnet is true and createPrivateEndpoint is true: Subnet CIDR prefix for private endpoints.')
param privateEndpointSubnetPrefix string = '10.30.4.0/27'

@description('When createVnet is true and createDnsResolver is true: Subnet CIDR prefix for DNS resolver inbound endpoint.')
param dnsResolverSubnetPrefix string = '10.30.3.0/27'

@description('Create a private endpoint subnet when createVnet is true.')
param createPrivateEndpoint bool = true

@description('Create a DNS private resolver with inbound endpoint when createVnet is true.')
param createDnsResolver bool = true

@description('Static IP for DNS resolver inbound endpoint (optional).')
param dnsResolverInboundIp string = ''

@description('When createVnet is true and createVpnGateway is true: GatewaySubnet CIDR prefix.')
param gatewaySubnetPrefix string = '10.30.255.0/27'

@description('Create a VPN gateway for Point-to-Site access when createVnet is true.')
param createVpnGateway bool = true

@description('Point-to-Site client address pool (must not overlap VNet/on-prem).')
param vpnClientAddressPool string = '172.16.200.0/24'

@description('VPN gateway SKU.')
param vpnGatewaySku string = 'VpnGw1'

@description('Azure AD tenant ID for P2S auth (OpenVPN).')
param aadTenantId string

@description('Azure AD audience for Azure VPN (Public cloud default).')
param aadAudience string = '41b23e61-6c1e-4545-b367-cd054e0ed4b4'

@description('Azure AD issuer for P2S auth. If empty, uses https://sts.windows.net/{tenantId}/')
param aadIssuer string = ''

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
    subnets: concat([
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
    ], createVpnGateway ? [
      {
        name: 'GatewaySubnet'
        properties: {
          addressPrefix: gatewaySubnetPrefix
        }
      }
    ] : [], createPrivateEndpoint ? [
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ] : [], createDnsResolver ? [
      {
        name: 'dns-resolver-inbound'
        properties: {
          addressPrefix: dnsResolverSubnetPrefix
          delegations: [
            {
              name: 'dnsresolver-delegation'
              properties: {
                serviceName: 'Microsoft.Network/dnsResolvers'
              }
            }
          ]
        }
      }
    ] : [])
  }
}

resource dnsResolver 'Microsoft.Network/dnsResolvers@2022-07-01' = if (createVnet && createDnsResolver) {
  name: 'dnsr-${resourceToken}'
  location: location
  tags: tags
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
  }
}

resource dnsResolverInbound 'Microsoft.Network/dnsResolvers/inboundEndpoints@2022-07-01' = if (createVnet && createDnsResolver) {
  parent: dnsResolver
  name: 'inbound'
  location: location
  properties: {
    ipConfigurations: [
      empty(dnsResolverInboundIp) ? {
        privateIpAllocationMethod: 'Dynamic'
        subnet: {
          id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'dns-resolver-inbound')
        }
      } : {
        privateIpAllocationMethod: 'Static'
        privateIpAddress: dnsResolverInboundIp
        subnet: {
          id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'dns-resolver-inbound')
        }
      }
    ]
  }
}

resource vpnGatewayPublicIp 'Microsoft.Network/publicIPAddresses@2023-11-01' = if (createVnet && createVpnGateway) {
  name: 'pip-vpngw-${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
  }
}

var resolvedAadIssuer = empty(aadIssuer) ? 'https://sts.windows.net/${aadTenantId}/' : aadIssuer

resource vpnGateway 'Microsoft.Network/virtualNetworkGateways@2023-11-01' = if (createVnet && createVpnGateway) {
  name: 'vpngw-${resourceToken}'
  location: location
  tags: tags
  properties: {
    ipConfigurations: [
      {
        name: 'vnetGatewayConfig'
        properties: {
          publicIPAddress: {
            id: vpnGatewayPublicIp.id
          }
          subnet: {
            id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'GatewaySubnet')
          }
        }
      }
    ]
    gatewayType: 'Vpn'
    vpnType: 'RouteBased'
    enableBgp: false
    sku: {
      name: vpnGatewaySku
      tier: vpnGatewaySku
    }
    vpnClientConfiguration: {
      vpnClientAddressPool: {
        addressPrefixes: [
          vpnClientAddressPool
        ]
      }
      vpnClientProtocols: [
        'OpenVPN'
      ]
      aadTenant: '${environment().authentication.loginEndpoint}${aadTenantId}'
      aadAudience: aadAudience
      aadIssuer: resolvedAadIssuer
    }
  }
}

output vnetId string = createVnet ? vnet.id : existingVnetResourceId
output acaInfrastructureSubnetId string = createVnet ? resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'aca-infra') : existingAcaInfrastructureSubnetId
output functionsIntegrationSubnetId string = createVnet ? resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'functions-integration') : existingFunctionsIntegrationSubnetId
output vpnGatewayName string = createVnet && createVpnGateway ? vpnGateway.name : ''
output privateEndpointSubnetId string = createPrivateEndpoint ? (createVnet ? resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'private-endpoints') : existingPrivateEndpointSubnetId) : ''
output dnsResolverInboundIp string = createVnet && createDnsResolver ? dnsResolverInbound!.properties.ipConfigurations[0].privateIpAddress : ''
