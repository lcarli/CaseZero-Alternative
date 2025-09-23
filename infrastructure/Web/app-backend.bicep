@description('Name of the backend application')
param appName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('App Service Plan ID')
param appServicePlanId string

@description('Environment name')
param environmentName string

@description('Resource tags')
param tags object = {}

@description('Database connection string')
param databaseConnectionString string = ''

@description('JWT signing key')
@secure()
param jwtSigningKey string = newGuid()

@description('CORS allowed origins')
param corsAllowedOrigins array = ['*']

// Backend API App Service
resource backendApp 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlanId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      cors: {
        allowedOrigins: corsAllowedOrigins
        supportCredentials: false
      }
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: !empty(databaseConnectionString) ? databaseConnectionString : 'Data Source=casezero.db'
        }
        {
          name: 'JwtSettings__SecretKey'
          value: jwtSigningKey
        }
        {
          name: 'JwtSettings__Issuer'
          value: 'CaseZeroApi'
        }
        {
          name: 'JwtSettings__Audience'
          value: 'CaseZeroClient'
        }
        {
          name: 'JwtSettings__ExpirationInMinutes'
          value: '60'
        }
        {
          name: 'IpRateLimiting__EnableEndpointRateLimiting'
          value: 'true'
        }
        {
          name: 'IpRateLimiting__StackBlockedRequests'
          value: 'false'
        }
        {
          name: 'IpRateLimiting__GeneralRules__0__Endpoint'
          value: '*'
        }
        {
          name: 'IpRateLimiting__GeneralRules__0__Period'
          value: '1m'
        }
        {
          name: 'IpRateLimiting__GeneralRules__0__Limit'
          value: '100'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
    httpsOnly: true
    clientAffinityEnabled: false
  }
}

// Application Insights for monitoring
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
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
resource backendAppConfig 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: backendApp
  name: 'appsettings'
  properties: {
    APPINSIGHTS_INSTRUMENTATIONKEY: appInsights.properties.InstrumentationKey
    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
    ApplicationInsights__InstrumentationKey: appInsights.properties.InstrumentationKey
  }
}

// Outputs
output appUrl string = 'https://${backendApp.properties.defaultHostName}'
output appName string = backendApp.name
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
