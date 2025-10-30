// ==============================================================================
// API Backend App Service Plan Module
// ==============================================================================
// Provisions Azure App Service Plan for API Backend
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('Name prefix for resources')
param namePrefix string

@description('App Service Plan SKU')
param sku object = {
  name: environment == 'prod' ? 'P1v3' : 'B1'
  tier: environment == 'prod' ? 'PremiumV3' : 'Basic'
  capacity: environment == 'prod' ? 2 : 1
}

@description('Enable zone redundancy')
param enableZoneRedundancy bool = environment == 'prod'

@description('Resource tags')
param tags object

var appServicePlanName = '${namePrefix}-api-plan-${environment}'

// App Service Plan (Linux)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: sku
  kind: 'linux'
  properties: {
    reserved: true // Linux
    zoneRedundant: enableZoneRedundancy
  }
}

// Outputs
output appServicePlanId string = appServicePlan.id
output appServicePlanName string = appServicePlan.name
