// ==============================================================================
// Function App Module for Case Generator (.NET 9.0)
// ==============================================================================
// Provisions Azure Function App for Case Generation workloads
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('Name prefix for resources')
param namePrefix string

@description('App Service Plan ID')
param appServicePlanId string

@description('Storage Account Connection String')
@secure()
param storageConnectionString string

@description('Application Insights Connection String')
param appInsightsConnectionString string = ''

@description('Application Insights Instrumentation Key')
param appInsightsInstrumentationKey string = ''

@description('Key Vault URI')
param keyVaultUri string

@description('Resource tags')
param tags object

var functionAppName = '${namePrefix}-func-${environment}'

// Function App (.NET 9.0 Isolated)
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      use32BitWorkerProcess: false
      alwaysOn: environment == 'prod' ? true : false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/api/health'
      functionsRuntimeScaleMonitoringEnabled: environment == 'prod' ? true : false
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: '${functionAppName}-content'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : (environment == 'staging' ? 'Staging' : 'Development')
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
        // Case Generator specific settings
        {
          name: 'CaseGeneratorStorage__ConnectionString'
          value: storageConnectionString
        }
        {
          name: 'CaseGeneratorStorage__CasesContainer'
          value: 'cases'
        }
        {
          name: 'CaseGeneratorStorage__BundlesContainer'
          value: 'bundles'
        }
        {
          name: 'KeyVault__VaultUri'
          value: keyVaultUri
        }
        {
          name: 'TaskHub'
          value: 'CaseGeneratorHub'
        }
        // Azure OpenAI settings (from Key Vault)
        {
          name: 'AzureOpenAI__Endpoint'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/azure-openai-endpoint/)'
        }
        {
          name: 'AzureOpenAI__ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/azure-openai-api-key/)'
        }
        {
          name: 'AzureOpenAI__DeploymentName'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/azure-openai-deployment-name/)'
        }
      ]
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppId string = functionApp.id
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output functionAppPrincipalId string = functionApp.identity.principalId
