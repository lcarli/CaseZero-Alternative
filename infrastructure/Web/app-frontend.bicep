@description('Name of the frontend application')
param appName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('App Service Plan ID')
param appServicePlanId string

@description('Environment name')
param environmentName string

@description('Resource tags')
param tags object = {}

@description('Backend API URL')
param backendApiUrl string

@description('Node.js version')
param nodeVersion string = '18-lts'

// Frontend Web App Service
resource frontendApp 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlanId
    siteConfig: {
      linuxFxVersion: 'NODE|${nodeVersion}'
      alwaysOn: true
      ftpsState: 'Disabled'
      appCommandLine: 'pm2 serve dist --spa'
      appSettings: [
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: nodeVersion
        }
        {
          name: 'VITE_API_BASE_URL'
          value: backendApiUrl
        }
        {
          name: 'VITE_APP_TITLE'
          value: 'CaseZero'
        }
        {
          name: 'NODE_ENV'
          value: environmentName == 'prod' ? 'production' : 'development'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'true'
        }
        {
          name: 'ENABLE_ORYX_BUILD'
          value: 'true'
        }
        {
          name: 'PRE_BUILD_SCRIPT_PATH'
          value: 'npm install'
        }
        {
          name: 'POST_BUILD_SCRIPT_PATH'
          value: 'npm run build'
        }
      ]
    }
    httpsOnly: true
    clientAffinityEnabled: false
  }
}

// Application Insights for frontend monitoring
resource frontendAppInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-insights'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

// Add Application Insights configuration to app settings
resource frontendAppConfig 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: frontendApp
  name: 'appsettings'
  properties: {
    APPINSIGHTS_INSTRUMENTATIONKEY: frontendAppInsights.properties.InstrumentationKey
    APPLICATIONINSIGHTS_CONNECTION_STRING: frontendAppInsights.properties.ConnectionString
    VITE_APPINSIGHTS_INSTRUMENTATIONKEY: frontendAppInsights.properties.InstrumentationKey
    VITE_APPINSIGHTS_CONNECTION_STRING: frontendAppInsights.properties.ConnectionString
  }
}

// Custom domain and SSL certificate (optional)
resource customDomain 'Microsoft.Web/sites/hostNameBindings@2023-01-01' = if (environmentName == 'prod') {
  parent: frontendApp
  name: '${appName}.azurewebsites.net'
  properties: {
    hostNameType: 'Verified'
    sslState: 'SniEnabled'
  }
}

// Outputs
output appUrl string = 'https://${frontendApp.properties.defaultHostName}'
output appName string = frontendApp.name
output appInsightsInstrumentationKey string = frontendAppInsights.properties.InstrumentationKey
