targetScope = 'subscription'

@description('Name of the environment (e.g., dev, staging, prod)')
param environmentName string

@description('Primary location for all resources')
param location string

@description('Principal ID of the deploying user for Key Vault access')
param principalId string = ''

@description('Azure AD Client ID for the consortium app')
param azureAdClientId string = ''

@description('Azure AD Client Secret for the consortium app')
@secure()
param azureAdClientSecret string = ''

@description('App Service Plan SKU')
@allowed(['B1', 'B2', 'S1', 'S2', 'P1v3'])
param appServicePlanSku string = 'B1'

@description('SQL Database SKU')
@allowed(['Basic', 'S0', 'S1', 'S2'])
param sqlDatabaseSku string = 'S0'

@description('Azure AI Search SKU')
@allowed(['free', 'basic', 'standard'])
param searchSku string = 'basic'

// Tags for all resources
var tags = {
  'azd-env-name': environmentName
  project: 'purview-consortium'
}

// Generate unique suffix for resource names
var resourceSuffix = take(uniqueString(subscription().id, environmentName, location), 6)
var resourceGroupName = 'rg-${environmentName}'
var kvName = take('kv${replace(environmentName, '-', '')}${resourceSuffix}', 24)

// Create the resource group
resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// Log Analytics Workspace
module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  scope: resourceGroup
  params: {
    name: 'log-${environmentName}-${resourceSuffix}'
    location: location
    tags: tags
  }
}

// Application Insights
module appInsights 'modules/app-insights.bicep' = {
  name: 'app-insights'
  scope: resourceGroup
  params: {
    name: 'appi-${environmentName}-${resourceSuffix}'
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

// Key Vault
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: resourceGroup
  params: {
    name: kvName
    location: location
    tags: tags
    principalId: principalId
  }
}

// Azure SQL Database
module sqlDatabase 'modules/sql-database.bicep' = {
  name: 'sql-database'
  scope: resourceGroup
  params: {
    name: 'sql-${environmentName}-${resourceSuffix}'
    location: 'eastus2'
    tags: tags
    databaseName: 'PurviewConsortium'
    sku: sqlDatabaseSku
    keyVaultName: keyVault.outputs.name
    aadAdminObjectId: principalId
  }
}

// Azure AI Search
module searchService 'modules/search-service.bicep' = {
  name: 'search-service'
  scope: resourceGroup
  params: {
    name: 'srch-${environmentName}-${resourceSuffix}'
    location: location
    tags: tags
    sku: searchSku
    keyVaultName: keyVault.outputs.name
  }
}

// App Service Plan + API App
module appService 'modules/app-service.bicep' = {
  name: 'app-service'
  scope: resourceGroup
  params: {
    name: 'app-${environmentName}-${resourceSuffix}'
    location: 'westcentralus'
    tags: tags
    sku: appServicePlanSku
    appInsightsConnectionString: appInsights.outputs.connectionString
    appInsightsInstrumentationKey: appInsights.outputs.instrumentationKey
    sqlConnectionString: sqlDatabase.outputs.connectionString
    searchEndpoint: searchService.outputs.endpoint
    searchApiKey: searchService.outputs.adminKey
    azureAdClientId: azureAdClientId
    azureAdClientSecret: azureAdClientSecret
    staticWebAppUrl: staticWebApp.outputs.url
    keyVaultUri: keyVault.outputs.uri
  }
}

// Static Web App for the React SPA
module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'static-web-app'
  scope: resourceGroup
  params: {
    name: 'stapp-${environmentName}-${resourceSuffix}'
    location: 'eastus2'
    tags: tags
  }
}

// Outputs for azd
output AZURE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_LOCATION string = location
output API_URL string = appService.outputs.url
output WEB_URL string = staticWebApp.outputs.url
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output APPLICATIONINSIGHTS_CONNECTION_STRING string = appInsights.outputs.connectionString
