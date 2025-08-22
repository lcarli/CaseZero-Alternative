@description('The environment name (dev, prod)')
param environment string = 'dev'

@description('The location for all resources')
param location string = resourceGroup().location

@description('The name prefix for all resources')
param namePrefix string = 'casezero'

@description('Enable Application Insights')
param enableApplicationInsights bool = true

var resourceNameSuffix = '-${environment}'
var tags = {
  Environment: environment
  Project: 'CaseZero'
  Component: 'CaseGenerator'
  CostCenter: 'IT'
  ManagedBy: 'GitHub-Actions'
}

// Log Analytics Workspace (required for Application Insights)
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (enableApplicationInsights) {
  name: '${namePrefix}-casegen-logs${resourceNameSuffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: environment == 'prod' ? 90 : 30
    features: {
      searchVersion: 1
      legacy: 0
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights for Case Generator
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (enableApplicationInsights) {
  name: '${namePrefix}-casegen-insights${resourceNameSuffix}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: enableApplicationInsights ? logAnalyticsWorkspace.id : null
    RetentionInDays: environment == 'prod' ? 90 : 30
    SamplingPercentage: environment == 'prod' ? 100 : 50
    DisableIpMasking: false
  }
}

// Key Vault for secrets management
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${namePrefix}-casegen-kv${resourceNameSuffix}-${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: environment == 'prod' ? 90 : 7
    enableRbacAuthorization: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// Storage Account for Case Generator
resource caseGeneratorStorage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'st${namePrefix}gen${environment}'
  location: location
  tags: union(tags, { Purpose: 'CaseGenerator' })
  sku: {
    name: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    dnsEndpointType: 'Standard'
    defaultToOAuthAuthentication: true
    publicNetworkAccess: 'Enabled'
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    networkAcls: {
      bypass: 'AzureServices'
      virtualNetworkRules: []
      ipRules: []
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
    encryption: {
      requireInfrastructureEncryption: false
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
        queue: {
          keyType: 'Account'
          enabled: true
        }
        table: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

// Blob containers for Case Generator
resource caseGeneratorBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: caseGeneratorStorage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: environment == 'prod' ? 30 : 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: environment == 'prod' ? 30 : 7
    }
  }
}

resource casesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: caseGeneratorBlobService
  name: 'cases'
  properties: {
    publicAccess: 'None'
  }
}

resource bundlesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: caseGeneratorBlobService
  name: 'bundles'
  properties: {
    publicAccess: 'None'
  }
}

// Function App Service Plan for Case Generator
resource functionAppServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${namePrefix}-casegen-func-asp${resourceNameSuffix}'
  location: location
  tags: union(tags, { Purpose: 'CaseGenerator' })
  sku: {
    name: environment == 'prod' ? 'EP1' : 'Y1'  // Premium for prod, Consumption for dev
    tier: environment == 'prod' ? 'ElasticPremium' : 'Dynamic'
  }
  properties: {
    reserved: false // Windows
    maximumElasticWorkerCount: environment == 'prod' ? 10 : 1
  }
  kind: 'functionapp'
}

// Function App for Case Generator
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${namePrefix}-casegen-func${resourceNameSuffix}'
  location: location
  tags: union(tags, { Purpose: 'CaseGenerator' })
  kind: 'functionapp'
  properties: {
    serverFarmId: functionAppServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: environment == 'prod' ? true : false
      functionsRuntimeScaleMonitoringEnabled: environment == 'prod' ? true : false
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

var applicationInsightsInstrumentationKey = enableApplicationInsights ? (applicationInsights.?properties.InstrumentationKey ?? '') : ''
var applicationInsightsConnectionString = enableApplicationInsights ? (applicationInsights.?properties.ConnectionString ?? '') : ''

// Function App Configuration
resource functionAppConfig 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: functionApp
  name: 'appsettings'
  properties: {
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: 'DefaultEndpointsProtocol=https;AccountName=${caseGeneratorStorage.name};AccountKey=${caseGeneratorStorage.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
    WEBSITE_CONTENTSHARE: '${functionApp.name}-content'
    AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${caseGeneratorStorage.name};AccountKey=${caseGeneratorStorage.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
    WEBSITE_RUN_FROM_PACKAGE: '1'
    ASPNETCORE_ENVIRONMENT: environment == 'prod' ? 'Production' : 'Development'
    APPINSIGHTS_INSTRUMENTATIONKEY: applicationInsightsInstrumentationKey
    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString
    // Case Generator specific settings
    CaseGeneratorStorage__ConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${caseGeneratorStorage.name};AccountKey=${caseGeneratorStorage.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
    CaseGeneratorStorage__CasesContainer: 'cases'
    CaseGeneratorStorage__BundlesContainer: 'bundles'
    KeyVault__VaultUri: keyVault.properties.vaultUri
    TaskHub: 'CaseGeneratorHub'
  }
}

// RBAC: Grant Function App access to Key Vault
resource keyVaultAccessPolicy 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// RBAC: Grant Function App access to Storage Account
resource storageAccessPolicy 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(caseGeneratorStorage.id, functionApp.id, 'Storage Blob Data Contributor')
  scope: caseGeneratorStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output functionAppId string = functionApp.id
output caseGeneratorStorageName string = caseGeneratorStorage.name
output caseGeneratorStorageId string = caseGeneratorStorage.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultId string = keyVault.id
output applicationInsightsName string = enableApplicationInsights ? applicationInsights.name : ''
output applicationInsightsConnectionString string = applicationInsightsConnectionString
output logAnalyticsWorkspaceName string = enableApplicationInsights ? logAnalyticsWorkspace.name : ''
output logAnalyticsWorkspaceId string = enableApplicationInsights ? logAnalyticsWorkspace.id : ''
