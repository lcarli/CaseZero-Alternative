// ==============================================================================
// Main Orchestrator - 3-Tier Architecture (Azure Verified Modules)
// ==============================================================================
// Orchestrates deployment of all CaseZero infrastructure layers using 100% AVM:
// - Shared: Key Vault, Log Analytics, App Insights, SQL (All AVM 0.9-0.20)
// - API: Backend API (.NET 8.0) - App Service Plan + App Service (AVM 0.4-0.14)
// - Functions: Case Generator (.NET 9.0) - Storage, Plan, Functions (AVM 0.4-0.17)
// - Frontend: Static Web App (React + Vite) - Native Azure Resource
// Reference: https://azure.github.io/Azure-Verified-Modules/
// ==============================================================================

targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string = 'canadacentral'

@description('Name prefix for all resources')
param namePrefix string = 'casezero'

@description('Enable SQL Database (optional)')
param enableSqlDatabase bool = false

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string = ''

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string = ''

@description('Enable monitoring (Application Insights + Log Analytics)')
param enableMonitoring bool = true

@description('GitHub Repository URL for Static Web App (optional)')
param repositoryUrl string = ''

@description('GitHub Branch name')
param branchName string = environment == 'prod' ? 'main' : 'develop'

// ==============================================================================
// Resource Groups
// ==============================================================================

// Shared Resource Group
resource sharedResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-shared-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'Shared'
    ManagedBy: 'Bicep-IaC'
  }
}

// API Resource Group
resource apiResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-api-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'API'
    ManagedBy: 'Bicep-IaC'
  }
}

// Functions Resource Group
resource functionsResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-func-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'Functions'
    ManagedBy: 'Bicep-IaC'
  }
}

// Frontend Resource Group
resource frontendResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-web-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'Frontend'
    ManagedBy: 'Bicep-IaC'
  }
}

// ==============================================================================
// Layer 1: Shared Infrastructure
// ==============================================================================
module sharedInfrastructure 'shared/main.bicep' = {
  name: 'shared-infrastructure-deployment'
  scope: sharedResourceGroup
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    enableSqlDatabase: enableSqlDatabase
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    enableMonitoring: enableMonitoring
  }
}

// ==============================================================================
// Layer 2: API Backend Infrastructure
// ==============================================================================
module apiInfrastructure 'api/main.bicep' = {
  name: 'api-infrastructure-deployment'
  scope: apiResourceGroup
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    appServicePlanSku: {
      name: environment == 'prod' ? 'P1v3' : 'B1'
      tier: environment == 'prod' ? 'PremiumV3' : 'Basic'
      capacity: environment == 'prod' ? 2 : 1
    }
    useSqlite: !enableSqlDatabase
    sqlConnectionString: enableSqlDatabase ? sharedInfrastructure.outputs.sqlConnectionString : ''
    keyVaultUri: sharedInfrastructure.outputs.keyVaultUri
    keyVaultId: sharedInfrastructure.outputs.keyVaultId
    appInsightsConnectionString: enableMonitoring ? sharedInfrastructure.outputs.connectionString : ''
    appInsightsInstrumentationKey: enableMonitoring ? sharedInfrastructure.outputs.instrumentationKey : ''
    corsAllowedOrigins: ['*'] // Update with specific origins in production
  }
}

// ==============================================================================
// Layer 3: Functions Infrastructure
// ==============================================================================
module functionsInfrastructure 'functions/main.bicep' = {
  name: 'functions-infrastructure-deployment'
  scope: functionsResourceGroup
  params: {
    environment: environment
    location: location
    namePrefix: 'casegen'
    functionAppPlanSku: {
      name: environment == 'prod' ? 'EP1' : 'Y1'
      tier: environment == 'prod' ? 'ElasticPremium' : 'Dynamic'
    }
    storageSku: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'
    containerNames: [
      'cases'
      'bundles'
      'case-context'
      'logs'
    ]
    keyVaultUri: sharedInfrastructure.outputs.keyVaultUri
    appInsightsConnectionString: enableMonitoring ? sharedInfrastructure.outputs.connectionString : ''
    appInsightsInstrumentationKey: enableMonitoring ? sharedInfrastructure.outputs.instrumentationKey : ''
  }
}

// ==============================================================================
// Layer 4: Frontend Infrastructure
// ==============================================================================
module frontendInfrastructure 'frontend/main.bicep' = {
  name: 'frontend-infrastructure-deployment'
  scope: frontendResourceGroup
  params: {
    environment: environment
    namePrefix: namePrefix
    staticWebAppSku: {
      name: environment == 'prod' ? 'Standard' : 'Free'
      tier: environment == 'prod' ? 'Standard' : 'Free'
    }
    backendApiUrl: apiInfrastructure.outputs.apiAppServiceUrl
    repositoryUrl: repositoryUrl
    branchName: branchName
  }
}

// ==============================================================================
// RBAC Assignments
// ==============================================================================
// Note: RBAC assignments managed by AVM modules automatically via managedIdentities parameter
// The API and Functions apps use System-Assigned Managed Identity
// Key Vault access is granted through RBAC authorization (enableRbacAuthorization: true)
// Storage access for Functions is granted through the AVM storage module

// ==============================================================================
// Outputs
// ==============================================================================

// Resource Group outputs
output sharedResourceGroupName string = sharedResourceGroup.name
output apiResourceGroupName string = apiResourceGroup.name
output functionsResourceGroupName string = functionsResourceGroup.name
output frontendResourceGroupName string = frontendResourceGroup.name

// Shared infrastructure outputs
output keyVaultName string = sharedInfrastructure.outputs.keyVaultName
output keyVaultUri string = sharedInfrastructure.outputs.keyVaultUri
output applicationInsightsName string = enableMonitoring ? sharedInfrastructure.outputs.applicationInsightsName : ''
output logAnalyticsWorkspaceName string = enableMonitoring ? sharedInfrastructure.outputs.logAnalyticsWorkspaceName : ''
output sqlServerName string = enableSqlDatabase ? sharedInfrastructure.outputs.sqlServerName : ''
output sqlServerFqdn string = enableSqlDatabase ? sharedInfrastructure.outputs.sqlServerFqdn : ''
output sqlDatabaseName string = enableSqlDatabase ? sharedInfrastructure.outputs.sqlDatabaseName : ''

// API infrastructure outputs
output apiAppServiceName string = apiInfrastructure.outputs.apiAppServiceName
output apiAppServiceUrl string = apiInfrastructure.outputs.apiAppServiceUrl

// Functions infrastructure outputs
output functionAppName string = functionsInfrastructure.outputs.functionAppName
output functionAppUrl string = functionsInfrastructure.outputs.functionAppUrl
output functionsStorageAccountName string = functionsInfrastructure.outputs.storageAccountName

// Frontend infrastructure outputs
output staticWebAppName string = frontendInfrastructure.outputs.staticWebAppName
output staticWebAppUrl string = frontendInfrastructure.outputs.staticWebAppUrl

// Summary
output deploymentSummary object = {
  environment: environment
  location: location
  frontendUrl: frontendInfrastructure.outputs.staticWebAppUrl
  apiUrl: apiInfrastructure.outputs.apiAppServiceUrl
  functionsUrl: functionsInfrastructure.outputs.functionAppUrl
  sqlEnabled: enableSqlDatabase
  monitoringEnabled: enableMonitoring
}
