// ==============================================================================
// Shared Infrastructure - Azure Verified Modules (AVM)
// ==============================================================================
// Provisions shared resources using only Azure Verified Modules:
// - Key Vault (avm/res/key-vault/vault:0.12.0)
// - Log Analytics (avm/res/operational-insights/workspace:0.9.1)
// - Application Insights (avm/res/insights/component:0.4.2)
// - SQL Server (avm/res/sql/server:0.20.3) - Optional
// ==============================================================================

targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name prefix for resources')
param namePrefix string = 'casezero'

@description('Enable SQL Database')
param enableSqlDatabase bool = false

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string = ''

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string = ''

@description('Enable Application Insights and Log Analytics')
param enableMonitoring bool = true

var tags = {
  Environment: environment
  Project: 'CaseZero'
  ManagedBy: 'Bicep-IaC'
  DeployedBy: 'GitHub-Actions'
  Layer: 'Shared'
}

var keyVaultName = 'kv-ca-${environment}-${uniqueString(resourceGroup().id)}'
var logAnalyticsName = '${namePrefix}-logs-${environment}'
var appInsightsName = '${namePrefix}-insights-${environment}'
var sqlServerName = '${namePrefix}-sql-${environment}'
var sqlDatabaseName = '${namePrefix}-db'

// Connection string for SQL Server (will be stored in Key Vault)
var sqlConnectionStringValue = enableSqlDatabase ? 'Server=tcp:${sqlServerName}.${az.environment().suffixes.sqlServerHostname},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;' : ''

// ==============================================================================
// Key Vault (AVM)
// ==============================================================================
module keyVault 'br/public:avm/res/key-vault/vault:0.12.0' = {
  name: 'keyvault-deployment'
  params: {
    name: keyVaultName
    location: location
    tags: tags
    enableRbacAuthorization: true
    enablePurgeProtection: environment == 'prod'
    softDeleteRetentionInDays: environment == 'prod' ? 90 : 7
    sku: 'standard'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    secrets: enableSqlDatabase ? [
      {
        name: 'sql-connection-string'
        value: sqlConnectionStringValue
      }
    ] : []
  }
}

// ==============================================================================
// Log Analytics Workspace (AVM)
// ==============================================================================
module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.9.1' = if (enableMonitoring) {
  name: 'loganalytics-deployment'
  params: {
    name: logAnalyticsName
    location: location
    tags: tags
    dataRetention: environment == 'prod' ? 90 : 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    skuName: 'PerGB2018'
  }
}

// ==============================================================================
// Application Insights (AVM)
// ==============================================================================
module appInsights 'br/public:avm/res/insights/component:0.4.2' = if (enableMonitoring) {
  name: 'appinsights-deployment'
  params: {
    name: appInsightsName
    location: location
    tags: tags
    workspaceResourceId: enableMonitoring ? logAnalytics.outputs.resourceId : ''
    applicationType: 'web'
    kind: 'web'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    samplingPercentage: environment == 'prod' ? 100 : 50
  }
}

// ==============================================================================
// SQL Server (AVM) - Optional
// ==============================================================================
module sqlServer 'br/public:avm/res/sql/server:0.20.3' = if (enableSqlDatabase && !empty(sqlAdminLogin) && !empty(sqlAdminPassword)) {
  name: 'sqlserver-deployment'
  params: {
    name: sqlServerName
    location: location
    tags: tags
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    databases: [
      {
        name: sqlDatabaseName
        collation: 'SQL_Latin1_General_CP1_CI_AS'
        availabilityZone: -1
        sku: {
          name: environment == 'prod' ? 'S1' : 'Basic'
          tier: environment == 'prod' ? 'Standard' : 'Basic'
        }
        maxSizeBytes: environment == 'prod' ? 268435456000 : 2147483648
        zoneRedundant: environment == 'prod'
        licenseType: 'LicenseIncluded'
        readScale: environment == 'prod' ? 'Enabled' : 'Disabled'
        requestedBackupStorageRedundancy: environment == 'prod' ? 'Geo' : 'Local'
      }
    ]
    firewallRules: [
      {
        name: 'AllowAzureServices'
        startIpAddress: '0.0.0.0'
        endIpAddress: '0.0.0.0'
      }
    ]
  }
}

// ==============================================================================
// Outputs
// ==============================================================================

// Key Vault outputs
output keyVaultName string = keyVault.outputs.name
output keyVaultId string = keyVault.outputs.resourceId
output keyVaultUri string = keyVault.outputs.uri

// Monitoring outputs
output logAnalyticsWorkspaceName string = enableMonitoring ? logAnalytics.outputs.name : ''
output logAnalyticsWorkspaceId string = enableMonitoring ? logAnalytics.outputs.resourceId : ''
output applicationInsightsName string = enableMonitoring ? appInsights.outputs.name : ''
output applicationInsightsId string = enableMonitoring ? appInsights.outputs.resourceId : ''
output instrumentationKey string = enableMonitoring ? appInsights.outputs.instrumentationKey : ''
output connectionString string = enableMonitoring ? appInsights.outputs.connectionString : ''

// SQL Database outputs
output sqlServerName string = enableSqlDatabase ? sqlServer.outputs.name : ''
output sqlServerFqdn string = enableSqlDatabase ? '${sqlServer.outputs.name}.${az.environment().suffixes.sqlServerHostname}' : ''
output sqlDatabaseName string = enableSqlDatabase ? sqlDatabaseName : ''
// Connection string will be retrieved from Key Vault secret 'sql-connection-string'
output sqlConnectionString string = ''
