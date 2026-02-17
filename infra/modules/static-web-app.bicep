targetScope = 'resourceGroup'

@description('Name of the Static Web App')
param name string

@description('Location for the resource')
param location string = resourceGroup().location

@description('Tags to apply')
param tags object = {}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: union(tags, {
    'azd-service-name': 'web'
  })
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

output id string = staticWebApp.id
output name string = staticWebApp.name
output url string = 'https://${staticWebApp.properties.defaultHostname}'
#disable-next-line outputs-should-not-contain-secrets
output deploymentToken string = staticWebApp.listSecrets().properties.apiKey
