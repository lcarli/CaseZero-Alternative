// ==============================================================================
// Function App Service Plan Module
// ==============================================================================
// Provisions Azure App Service Plan for Functions
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('Name prefix for resources')
param namePrefix string

@description('Function App Plan SKU')
param sku object = {
  name: environment == 'prod' ? 'EP1' : 'Y1'
  tier: environment == 'prod' ? 'ElasticPremium' : 'Dynamic'
}

@description('Maximum elastic worker count')
param maximumElasticWorkerCount int = environment == 'prod' ? 10 : 1

@description('Resource tags')
param tags object

var functionAppServicePlanName = '${namePrefix}-func-plan-${environment}'

// Function App Service Plan
resource functionAppServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: functionAppServicePlanName
  location: location
  tags: tags
  sku: sku
  kind: 'linux'
  properties: {
    reserved: true // Linux
    maximumElasticWorkerCount: maximumElasticWorkerCount
  }
}

// Outputs
output functionAppServicePlanId string = functionAppServicePlan.id
output functionAppServicePlanName string = functionAppServicePlan.name
