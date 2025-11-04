// ==============================================================================
// SQL Database Module (Optional)
// ==============================================================================
// Provisions Azure SQL Server and Database with configurable SKUs
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('Name prefix for resources')
param namePrefix string

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('Enable SQL Database')
param enableSqlDatabase bool = true

@description('SQL Database SKU')
param sqlDatabaseSku object = {
  name: environment == 'prod' ? 'S1' : 'Basic'
  tier: environment == 'prod' ? 'Standard' : 'Basic'
  capacity: environment == 'prod' ? 20 : 5
}

@description('SQL Database max size in bytes')
param sqlDatabaseMaxSizeBytes int = environment == 'prod' ? 268435456000 : 2147483648 // 250GB for prod, 2GB for dev

@description('Enable zone redundancy for production')
param enableZoneRedundancy bool = environment == 'prod'

@description('Resource tags')
param tags object

var sqlServerName = '${namePrefix}-sql-${environment}'
var sqlDatabaseName = '${namePrefix}-db'

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = if (enableSqlDatabase) {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = if (enableSqlDatabase) {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: sqlDatabaseSku
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: sqlDatabaseMaxSizeBytes
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: enableZoneRedundancy
    licenseType: 'LicenseIncluded'
    readScale: environment == 'prod' ? 'Enabled' : 'Disabled'
    requestedBackupStorageRedundancy: environment == 'prod' ? 'Geo' : 'Local'
  }
}

// Firewall Rule: Allow Azure services
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = if (enableSqlDatabase) {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Outputs
output sqlServerName string = enableSqlDatabase ? sqlServer!.name : ''
output sqlServerFqdn string = enableSqlDatabase ? sqlServer!.properties.fullyQualifiedDomainName : ''
output sqlDatabaseName string = enableSqlDatabase ? sqlDatabase!.name : ''
output sqlServerId string = enableSqlDatabase ? sqlServer!.id : ''
output sqlDatabaseId string = enableSqlDatabase ? sqlDatabase!.id : ''
// Note: Connection string removed from outputs for security reasons
// Use Key Vault references in application settings instead
output connectionString string = ''
