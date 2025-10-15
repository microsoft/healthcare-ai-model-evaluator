param location string
param tags object = {}
param name string

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2021-09-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

output name string = containerRegistry.name
output loginServer string = containerRegistry.properties.loginServer 
