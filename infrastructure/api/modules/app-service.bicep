// ==============================================================================
// API Backend App Service Module (.NET 8.0)
// ==============================================================================
// Provisions Azure App Service for CaseZero API Backend
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('Name prefix for resources')
param namePrefix string

@description('App Service Plan ID')
param appServicePlanId string

@description('Application Insights Connection String')
param appInsightsConnectionString string = ''

@description('Application Insights Instrumentation Key')
param appInsightsInstrumentationKey string = ''

@description('Key Vault URI for secrets')
param keyVaultUri string

@description('SQL Connection String (optional)')
@secure()
param sqlConnectionString string = ''

@description('Use SQLite instead of SQL Server')
param useSqlite bool = true

@description('CORS allowed origins')
param corsAllowedOrigins array = ['*']

@description('Resource tags')
param tags object

var appName = '${namePrefix}-api-${environment}'
var defaultConnectionString = useSqlite ? 'Data Source=casezero.db' : sqlConnectionString

// API App Service (.NET 8.0)
resource apiAppService 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: environment == 'prod' ? true : false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/health'
      cors: {
        allowedOrigins: corsAllowedOrigins
        supportCredentials: false
      }
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: defaultConnectionString
          type: useSqlite ? 'Custom' : 'SQLAzure'
        }
      ]
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : (environment == 'staging' ? 'Staging' : 'Development')
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'JwtSettings__SecretKey'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/jwt-signing-key/)'
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
          value: environment == 'prod' ? '60' : '1440'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'IpRateLimiting__EnableEndpointRateLimiting'
          value: 'true'
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
          value: environment == 'prod' ? '100' : '200'
        }
      ]
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Outputs
output apiAppServiceName string = apiAppService.name
output apiAppServiceId string = apiAppService.id
output apiAppServiceUrl string = 'https://${apiAppService.properties.defaultHostName}'
output apiAppServicePrincipalId string = apiAppService.identity.principalId
