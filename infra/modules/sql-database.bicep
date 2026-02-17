targetScope = 'resourceGroup'

@description('Base name for SQL resources')
param name string

@description('Location for the resource')
param location string = resourceGroup().location

@description('Tags to apply')
param tags object = {}

@description('Name of the database')
param databaseName string

@description('SQL Database SKU')
@allowed(['Basic', 'S0', 'S1', 'S2'])
param sku string = 'S0'

@description('Key Vault name to store connection string')
param keyVaultName string = ''

@description('Azure AD admin object ID (the deploying user)')
param aadAdminObjectId string

@description('Azure AD admin login name')
param aadAdminLogin string = 'admin@fauxuni.org'

var skuMap = {
  Basic: { name: 'Basic', tier: 'Basic', capacity: 5 }
  S0: { name: 'S0', tier: 'Standard', capacity: 10 }
  S1: { name: 'S1', tier: 'Standard', capacity: 20 }
  S2: { name: 'S2', tier: 'Standard', capacity: 50 }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: aadAdminLogin
      sid: aadAdminObjectId
      tenantId: tenant().tenantId
      azureADOnlyAuthentication: true
      principalType: 'User'
    }
  }
}

// Allow Azure services to access
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: skuMap[sku].name
    tier: skuMap[sku].tier
    capacity: skuMap[sku].capacity
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB
    zoneRedundant: false
  }
}

// Store connection string in Key Vault
resource existingKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = if (!empty(keyVaultName)) {
  name: keyVaultName
}

resource connectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(keyVaultName)) {
  parent: existingKeyVault
  name: 'ConnectionStrings--DefaultConnection'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;'
  }
}

var connectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;'

output serverName string = sqlServer.name
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output connectionString string = connectionString
