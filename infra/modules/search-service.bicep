targetScope = 'resourceGroup'

@description('Name of the search service')
param name string

@description('Location for the resource')
param location string = resourceGroup().location

@description('Tags to apply')
param tags object = {}

@description('Search service SKU')
@allowed(['free', 'basic', 'standard'])
param sku string = 'basic'

@description('Key Vault name to store API key')
param keyVaultName string = ''

resource searchService 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

// Store the admin key in Key Vault
resource existingKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = if (!empty(keyVaultName)) {
  name: keyVaultName
}

resource searchKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(keyVaultName)) {
  parent: existingKeyVault
  name: 'AzureAISearch--ApiKey'
  properties: {
    value: searchService.listAdminKeys().primaryKey
  }
}

output id string = searchService.id
output name string = searchService.name
output endpoint string = 'https://${searchService.name}.search.windows.net'
#disable-next-line outputs-should-not-contain-secrets
output adminKey string = searchService.listAdminKeys().primaryKey
