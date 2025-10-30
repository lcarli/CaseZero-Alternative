// ==============================================================================
// Functions Infrastructure - Main Template
// ==============================================================================
// Provisions CaseGen.Functions (.NET 9.0) infrastructure
// ==============================================================================

targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name prefix for resources')
param namePrefix string = 'casegen'

@description('Function App Plan SKU')
param functionAppPlanSku object = {
  name: environment == 'prod' ? 'EP1' : 'Y1'
  tier: environment == 'prod' ? 'ElasticPremium' : 'Dynamic'
}

@description('Storage Account SKU')
param storageSku string = environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'

@description('Blob containers to create')
param containerNames array = [
  'cases'
  'bundles'
  'case-context'
  'logs'
]

@description('Key Vault URI from shared infrastructure')
param keyVaultUri string

@description('Application Insights Connection String from shared infrastructure')
param appInsightsConnectionString string = ''

@description('Application Insights Instrumentation Key from shared infrastructure')
param appInsightsInstrumentationKey string = ''

var tags = {
  Environment: environment
  Project: 'CaseZero'
  ManagedBy: 'Bicep-IaC'
  DeployedBy: 'GitHub-Actions'
  Layer: 'Functions'
  Component: 'CaseGenerator'
  Runtime: 'dotnet-isolated-9.0'
}

// ==============================================================================
// Storage Account for Case Generator
// ==============================================================================
module storageAccount 'modules/storage-account.bicep' = {
  name: 'functions-storage-deployment'
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    storageSku: storageSku
    containerNames: containerNames
    enableVersioning: environment == 'prod'
    retentionDays: environment == 'prod' ? 30 : 7
    tags: tags
  }
}

// ==============================================================================
// Function App Service Plan
// ==============================================================================
module functionAppServicePlan 'modules/function-app-plan.bicep' = {
  name: 'functions-app-service-plan-deployment'
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    sku: functionAppPlanSku
    maximumElasticWorkerCount: environment == 'prod' ? 10 : 1
    tags: tags
  }
}

// ==============================================================================
// Function App (.NET 9.0 Isolated)
// ==============================================================================

// Get storage account connection string using existing resource
resource existingStorage 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: take('st${namePrefix}${environment}${uniqueString(resourceGroup().id)}', 24)
}

module functionApp 'modules/function-app.bicep' = {
  name: 'functions-app-deployment'
  dependsOn: [storageAccount]
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    appServicePlanId: functionAppServicePlan.outputs.functionAppServicePlanId
    storageConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${existingStorage.name};AccountKey=${existingStorage.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
    appInsightsConnectionString: appInsightsConnectionString
    appInsightsInstrumentationKey: appInsightsInstrumentationKey
    keyVaultUri: keyVaultUri
    tags: tags
  }
}

// ==============================================================================
// Outputs
// ==============================================================================
output storageAccountName string = storageAccount.outputs.storageAccountName
output storageAccountId string = storageAccount.outputs.storageAccountId
output functionAppServicePlanId string = functionAppServicePlan.outputs.functionAppServicePlanId
output functionAppServicePlanName string = functionAppServicePlan.outputs.functionAppServicePlanName
output functionAppName string = functionApp.outputs.functionAppName
output functionAppId string = functionApp.outputs.functionAppId
output functionAppUrl string = functionApp.outputs.functionAppUrl
output functionAppPrincipalId string = functionApp.outputs.functionAppPrincipalId
