// ==============================================================================
// Shared Infrastructure - Main Template
// ==============================================================================
// Provisions shared resources: Key Vault, Monitoring, and optional SQL Database
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

// ==============================================================================
// Key Vault
// ==============================================================================
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    enableRbacAuthorization: true
    softDeleteRetentionInDays: environment == 'prod' ? 90 : 7
    tags: tags
  }
}

// ==============================================================================
// Monitoring (Log Analytics + Application Insights)
// ==============================================================================
module monitoring 'modules/monitoring.bicep' = if (enableMonitoring) {
  name: 'monitoring-deployment'
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    enableApplicationInsights: true
    retentionInDays: environment == 'prod' ? 90 : 30
    samplingPercentage: environment == 'prod' ? 100 : 50
    tags: tags
  }
}

// ==============================================================================
// SQL Database (Optional)
// ==============================================================================
module sqlDatabase 'modules/sql-database.bicep' = if (enableSqlDatabase && !empty(sqlAdminLogin) && !empty(sqlAdminPassword)) {
  name: 'sql-database-deployment'
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    enableSqlDatabase: enableSqlDatabase
    sqlDatabaseSku: {
      name: environment == 'prod' ? 'S1' : 'Basic'
      tier: environment == 'prod' ? 'Standard' : 'Basic'
      capacity: environment == 'prod' ? 20 : 5
    }
    sqlDatabaseMaxSizeBytes: environment == 'prod' ? 268435456000 : 2147483648
    enableZoneRedundancy: environment == 'prod'
    tags: tags
  }
}

// ==============================================================================
// Outputs
// ==============================================================================

// Key Vault outputs
output keyVaultName string = keyVault.outputs.keyVaultName
output keyVaultId string = keyVault.outputs.keyVaultId
output keyVaultUri string = keyVault.outputs.keyVaultUri

// Monitoring outputs
output logAnalyticsWorkspaceName string = enableMonitoring ? (monitoring.?outputs.logAnalyticsWorkspaceName ?? '') : ''
output logAnalyticsWorkspaceId string = enableMonitoring ? (monitoring.?outputs.logAnalyticsWorkspaceId ?? '') : ''
output applicationInsightsName string = enableMonitoring ? (monitoring.?outputs.applicationInsightsName ?? '') : ''
output applicationInsightsId string = enableMonitoring ? (monitoring.?outputs.applicationInsightsId ?? '') : ''
output instrumentationKey string = enableMonitoring ? (monitoring.?outputs.instrumentationKey ?? '') : ''
output connectionString string = enableMonitoring ? (monitoring.?outputs.connectionString ?? '') : ''

// SQL Database outputs
output sqlServerName string = enableSqlDatabase ? (sqlDatabase.?outputs.sqlServerName ?? '') : ''
output sqlServerFqdn string = enableSqlDatabase ? (sqlDatabase.?outputs.sqlServerFqdn ?? '') : ''
output sqlDatabaseName string = enableSqlDatabase ? (sqlDatabase.?outputs.sqlDatabaseName ?? '') : ''
output sqlConnectionString string = enableSqlDatabase ? (sqlDatabase.?outputs.connectionString ?? '') : ''
