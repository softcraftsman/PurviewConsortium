targetScope = 'resourceGroup'

@description('Base name for App Service resources')
param name string

@description('Location for the resource')
param location string = resourceGroup().location

@description('Tags to apply')
param tags object = {}

@description('App Service Plan SKU')
@allowed(['B1', 'B2', 'S1', 'S2', 'P1v3'])
param sku string = 'B1'

@description('Application Insights connection string')
param appInsightsConnectionString string = ''

@description('Application Insights instrumentation key')
param appInsightsInstrumentationKey string = ''

@description('SQL Database connection string')
@secure()
param sqlConnectionString string = ''

@description('Azure AI Search endpoint')
param searchEndpoint string = ''

@description('Azure AI Search API key')
@secure()
param searchApiKey string = ''

@description('Azure AD Client ID')
param azureAdClientId string = ''

@description('Azure AD Client Secret')
@secure()
param azureAdClientSecret string = ''

@description('Static Web App URL for CORS')
param staticWebAppUrl string = ''

@description('Key Vault URI')
param keyVaultUri string = ''

var planName = 'plan-${name}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: sku
  }
  properties: {
    reserved: true // Linux
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: union(tags, {
    'azd-service-name': 'api'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/healthz'
      cors: {
        allowedOrigins: [
          staticWebAppUrl
          'http://localhost:5173'
        ]
        supportCredentials: true
      }
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsights__InstrumentationKey'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'AzureAd__Instance'
          value: environment().authentication.loginEndpoint
        }
        {
          name: 'AzureAd__TenantId'
          value: 'common'
        }
        {
          name: 'AzureAd__ClientId'
          value: azureAdClientId
        }
        {
          name: 'AzureAd__ClientSecret'
          value: azureAdClientSecret
        }
        {
          name: 'AzureAd__Audience'
          value: azureAdClientId
        }
        {
          name: 'AzureAISearch__Endpoint'
          value: searchEndpoint
        }
        {
          name: 'AzureAISearch__ApiKey'
          value: searchApiKey
        }
        {
          name: 'AzureAISearch__IndexName'
          value: 'consortium-catalog'
        }
        {
          name: 'Cors__AllowedOrigins__0'
          value: staticWebAppUrl
        }
        {
          name: 'KeyVault__Uri'
          value: keyVaultUri
        }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
      ]
    }
  }
}

output id string = appService.id
output name string = appService.name
output url string = 'https://${appService.properties.defaultHostName}'
output principalId string = appService.identity.principalId
