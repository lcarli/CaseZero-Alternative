// ==============================================================================
// API Backend Infrastructure - Main Template
// ==============================================================================
// Provisions CaseZero API Backend (.NET 8.0) infrastructure
// ==============================================================================

targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name prefix for resources')
param namePrefix string = 'casezero'

@description('App Service Plan SKU')
param appServicePlanSku object = {
  name: environment == 'prod' ? 'P1v3' : 'B1'
  tier: environment == 'prod' ? 'PremiumV3' : 'Basic'
  capacity: environment == 'prod' ? 2 : 1
}

@description('Use SQLite instead of SQL Server')
param useSqlite bool = true

@description('SQL Connection String (required if useSqlite = false)')
@secure()
param sqlConnectionString string = ''

@description('Key Vault URI from shared infrastructure')
param keyVaultUri string

@description('Application Insights Connection String from shared infrastructure')
param appInsightsConnectionString string = ''

@description('Application Insights Instrumentation Key from shared infrastructure')
param appInsightsInstrumentationKey string = ''

@description('CORS allowed origins')
param corsAllowedOrigins array = ['*']

var tags = {
  Environment: environment
  Project: 'CaseZero'
  ManagedBy: 'Bicep-IaC'
  DeployedBy: 'GitHub-Actions'
  Layer: 'API'
  Component: 'Backend'
  Runtime: 'dotnet-8.0'
}

// ==============================================================================
// App Service Plan
// ==============================================================================
module appServicePlan 'modules/app-service-plan.bicep' = {
  name: 'api-app-service-plan-deployment'
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    sku: appServicePlanSku
    enableZoneRedundancy: environment == 'prod'
    tags: tags
  }
}

// ==============================================================================
// API App Service (.NET 8.0)
// ==============================================================================
module apiAppService 'modules/app-service.bicep' = {
  name: 'api-app-service-deployment'
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    appServicePlanId: appServicePlan.outputs.appServicePlanId
    appInsightsConnectionString: appInsightsConnectionString
    appInsightsInstrumentationKey: appInsightsInstrumentationKey
    keyVaultUri: keyVaultUri
    sqlConnectionString: sqlConnectionString
    useSqlite: useSqlite
    corsAllowedOrigins: corsAllowedOrigins
    tags: tags
  }
}

// ==============================================================================
// Outputs
// ==============================================================================
output appServicePlanId string = appServicePlan.outputs.appServicePlanId
output appServicePlanName string = appServicePlan.outputs.appServicePlanName
output apiAppServiceName string = apiAppService.outputs.apiAppServiceName
output apiAppServiceId string = apiAppService.outputs.apiAppServiceId
output apiAppServiceUrl string = apiAppService.outputs.apiAppServiceUrl
output apiAppServicePrincipalId string = apiAppService.outputs.apiAppServicePrincipalId
