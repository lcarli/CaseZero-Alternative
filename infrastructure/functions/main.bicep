// ==============================================================================
// Functions Infrastructure - Main Template (using Azure Verified Modules)
// ==============================================================================
// Provisions CaseGen.Functions (.NET 9.0) infrastructure using AVM
// - Storage Account (avm/res/storage/storage-account:0.17.1)
// - App Service Plan (avm/res/web/serverfarm:0.4.1)
// - Function App (avm/res/web/site:0.14.0)
// Reference: https://azure.github.io/Azure-Verified-Modules/
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

@description('Key Vault Resource ID from shared infrastructure (used to grant the Function App MI access to secrets)')
param keyVaultId string

@description('Application Insights Connection String from shared infrastructure')
param appInsightsConnectionString string = ''

@description('Application Insights Instrumentation Key from shared infrastructure')
param appInsightsInstrumentationKey string = ''

// Variables
var tags = {
  Environment: environment
  Project: 'CaseZero'
  ManagedBy: 'Bicep-IaC'
  DeployedBy: 'GitHub-Actions'
  Layer: 'Functions'
  Component: 'CaseGenerator'
  Runtime: 'dotnet-isolated-9.0'
}

var storageAccountName = 'st${toLower(take(namePrefix, 2))}${toLower(environment)}${take(uniqueString(resourceGroup().id), 10)}'
var functionAppName = '${namePrefix}-func-${environment}'
var appServicePlanName = '${namePrefix}-funcplan-${environment}'

// ==============================================================================
// Storage Account for Case Generator (AVM)
// ==============================================================================
module storageAccount 'br/public:avm/res/storage/storage-account:0.17.1' = {
  name: 'functions-storage-deployment'
  params: {
    name: storageAccountName
    location: location
    skuName: storageSku
    kind: 'StorageV2'
    allowSharedKeyAccess: true
    publicNetworkAccess: 'Enabled'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    blobServices: {
      containers: [for containerName in containerNames: {
        name: containerName
        publicAccess: 'None'
      }]
      deleteRetentionPolicyEnabled: true
      deleteRetentionPolicyDays: environment == 'prod' ? 30 : 7
    }
    tags: tags
  }
}

// ==============================================================================
// App Service Plan for Functions (AVM)
// ==============================================================================
module appServicePlan 'br/public:avm/res/web/serverfarm:0.4.1' = {
  name: 'functions-app-service-plan-deployment'
  params: {
    name: appServicePlanName
    location: location
    kind: 'linux'
    skuName: functionAppPlanSku.name
    skuCapacity: functionAppPlanSku.tier == 'Dynamic' ? 0 : (environment == 'prod' ? 3 : 1)
    reserved: true // Linux
    maximumElasticWorkerCount: environment == 'prod' ? 10 : 1
    tags: tags
  }
}

// Get storage account key for connection string
resource existingStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
  dependsOn: [storageAccount]
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${existingStorageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'

// ==============================================================================
// Function App (.NET 9.0 Isolated) (AVM)
// ==============================================================================
module functionApp 'br/public:avm/res/web/site:0.14.0' = {
  name: 'functions-app-deployment'
  params: {
    name: functionAppName
    location: location
    kind: 'functionapp,linux'
    serverFarmResourceId: appServicePlan.outputs.resourceId
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    managedIdentities: {
      systemAssigned: true
    }
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      use32BitWorkerProcess: false
      alwaysOn: environment == 'prod'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/api/health'
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
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
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
        {
          name: 'CaseGeneratorStorage__ConnectionString'
          value: storageConnectionString
        }
        {
          name: 'CaseGeneratorStorage__AccountName'
          value: storageAccountName
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
        {
          name: 'LLM__UseAzureFoundry'
          value: 'true'
        }
        {
          name: 'AzureFoundry__Endpoint'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/azure-foundry-endpoint/)'
        }
        {
          name: 'AzureFoundry__ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/azure-foundry-api-key/)'
        }
        {
          name: 'AzureFoundry__ModelName'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/azure-foundry-model-name/)'
        }
        {
          name: 'AzureFoundry__ImageDeploymentName'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/azure-foundry-image-deployment-name/)'
        }
      ]
    }
    tags: tags
  }
}

// ==============================================================================
// Forensic Request Queue
// ==============================================================================
// Pre-create the queue used by the API (ForensicQueueService) so the API can
// run with least-privilege RBAC (Storage Queue Data Message Sender) without
// needing queue-management permissions at startup.
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: existingStorageAccount
  name: 'default'
  dependsOn: [storageAccount]
}

resource forensicRequestsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'forensic-requests'
}

// ==============================================================================
// RBAC - Grant Function App MI access to Key Vault secrets
// ==============================================================================
// Shares the same module used by the API layer. The Function App's MI needs
// "Key Vault Secrets User" so it can resolve the @Microsoft.KeyVault(...)
// references in AzureFoundry__* app settings.
module functionsKeyVaultRoleAssignment '../api/keyvault-rbac.bicep' = {
  name: 'functions-keyvault-rbac-deployment'
  scope: resourceGroup(split(keyVaultId, '/')[2], split(keyVaultId, '/')[4])
  params: {
    keyVaultId: keyVaultId
    principalId: functionApp.outputs.systemAssignedMIPrincipalId!
    principalType: 'ServicePrincipal'
  }
}

// ==============================================================================
// Outputs
// ==============================================================================
output storageAccountName string = storageAccount.outputs.name
output storageAccountId string = storageAccount.outputs.resourceId
output functionAppServicePlanId string = appServicePlan.outputs.resourceId
output functionAppServicePlanName string = appServicePlan.outputs.name
output functionAppName string = functionApp.outputs.name
output functionAppId string = functionApp.outputs.resourceId
output functionAppUrl string = 'https://${functionApp.outputs.defaultHostname}'
output functionAppPrincipalId string = functionApp.outputs.systemAssignedMIPrincipalId!
