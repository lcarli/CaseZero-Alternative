// ==============================================================================
// API Backend Infrastructure - Main Template (using Azure Verified Modules)
// ==============================================================================
// Provisions CaseZero API Backend (.NET 8.0) infrastructure using 100% AVM
// - App Service Plan (avm/res/web/serverfarm:0.4.1)
// - App Service (avm/res/web/site:0.14.0)
// Reference: https://azure.github.io/Azure-Verified-Modules/
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

@description('Key Vault Resource ID from shared infrastructure')
param keyVaultId string

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

var appServicePlanName = '${namePrefix}-api-plan-${environment}'
var apiAppName = '${namePrefix}-api-${environment}'
// For SQLite, use local file. For SQL Server, use Key Vault secret reference
var defaultConnectionString = useSqlite ? 'Data Source=casezero.db' : '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/sql-connection-string/)'

// ==============================================================================
// App Service Plan (AVM)
// ==============================================================================
module appServicePlan 'br/public:avm/res/web/serverfarm:0.4.1' = {
  name: 'api-app-service-plan-deployment'
  params: {
    name: appServicePlanName
    location: location
    tags: tags
    kind: 'linux'
    skuName: appServicePlanSku.name
    skuCapacity: appServicePlanSku.capacity
    reserved: true
    zoneRedundant: environment == 'prod'
  }
}

// ==============================================================================
// API App Service (.NET 8.0) (AVM)
// ==============================================================================
module apiAppService 'br/public:avm/res/web/site:0.14.0' = {
  name: 'api-app-service-deployment'
  params: {
    name: apiAppName
    location: location
    tags: tags
    kind: 'app,linux'
    serverFarmResourceId: appServicePlan.outputs.resourceId
    httpsOnly: true
    clientAffinityEnabled: false
    managedIdentities: {
      systemAssigned: true
    }
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: environment == 'prod'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/health'
      cors: {
        allowedOrigins: corsAllowedOrigins
        supportCredentials: false
      }
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
      // Connection Strings
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: defaultConnectionString
          type: useSqlite ? 'Custom' : 'SQLAzure'
        }
      ]
    }
  }
}

// ==============================================================================
// RBAC - Grant API App Service access to Key Vault Secrets
// ==============================================================================
// Deploy role assignment in shared resource group where Key Vault exists
module keyVaultRoleAssignment 'keyvault-rbac.bicep' = {
  name: 'api-keyvault-rbac-deployment'
  scope: resourceGroup(split(keyVaultId, '/')[2], split(keyVaultId, '/')[4]) // Extract subscription and RG from Key Vault ID
  params: {
    keyVaultId: keyVaultId
    principalId: apiAppService.outputs.systemAssignedMIPrincipalId!
    principalType: 'ServicePrincipal'
  }
}

// ==============================================================================
// Outputs
// ==============================================================================
output appServicePlanId string = appServicePlan.outputs.resourceId
output appServicePlanName string = appServicePlan.outputs.name
output apiAppServiceName string = apiAppService.outputs.name
output apiAppServiceId string = apiAppService.outputs.resourceId
output apiAppServiceUrl string = 'https://${apiAppService.outputs.defaultHostname}'
output apiAppServicePrincipalId string = apiAppService.outputs.systemAssignedMIPrincipalId!
