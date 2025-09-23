targetScope = 'resourceGroup'

@description('Environment name (e.g., dev, staging, prod)')
param environmentName string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the application')
param appName string = 'casezero'

@description('SKU for App Service Plan')
param appServicePlanSku string = 'B1'

@description('Base name for resources')
param resourceToken string = toLower(uniqueString(subscription().id, environmentName, location))

// Variables
var tags = {
  'azd-env-name': environmentName
}

var appServicePlanName = 'plan-${appName}-${resourceToken}'
var backendAppName = 'app-${appName}-api-${resourceToken}'
var frontendAppName = 'app-${appName}-web-${resourceToken}'

// App Service Plan

module appServicePlan 'br/public:avm/res/web/serverfarm:0.5.0' = {
  params: {
    name: appServicePlanName
    location: location
    tags: tags
    kind: 'linux'
    skuName: appServicePlanSku
  }
}

// Backend API App Service
module backendApp 'app-backend.bicep' = {
  name: 'backend-deployment'
  params: {
    appName: backendAppName
    location: location
    appServicePlanId: appServicePlan.outputs.resourceId
    environmentName: environmentName
    tags: tags
  }
}

// Frontend Web App Service
module frontendApp 'app-frontend.bicep' = {
  name: 'frontend-deployment'
  params: {
    appName: frontendAppName
    location: location
    appServicePlanId: appServicePlan.outputs.resourceId
    environmentName: environmentName
    tags: tags
    backendApiUrl: backendApp.outputs.appUrl
  }
}

module storage 'br/public:avm/res/storage/storage-account:0.26.2' = {
  params:{
    name: 'st${appName}${environmentName}001'
  }
}

// Outputs
output backendUrl string = backendApp.outputs.appUrl
output frontendUrl string = frontendApp.outputs.appUrl
output storageAccountName string = storage.outputs.name
output resourceGroupName string = resourceGroup().name
