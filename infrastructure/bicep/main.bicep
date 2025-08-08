@description('The environment name (dev, prod)')
param environment string = 'dev'

@description('The location for all resources')
param location string = resourceGroup().location

@description('The name prefix for all resources')
param namePrefix string = 'casezero'

@description('SQL Server administrator login')
@secure()
param sqlServerAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlServerAdminPassword string

@description('The pricing tier for the App Service Plan')
@allowed([
  'B1'  // Basic - Cost optimized for dev
  'B2'  // Basic
  'S1'  // Standard - Production minimum
  'S2'  // Standard
  'P1v2' // Premium - Production recommended
])
param appServicePlanSku string = environment == 'prod' ? 'S1' : 'B1'

@description('Enable Application Insights')
param enableApplicationInsights bool = true

@description('Enable backup for production')
param enableBackup bool = environment == 'prod'

var resourceNameSuffix = '-${environment}'
var tags = {
  Environment: environment
  Project: 'CaseZero'
  CostCenter: 'IT'
  ManagedBy: 'GitHub-Actions'
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${namePrefix}-asp${resourceNameSuffix}'
  location: location
  tags: tags
  sku: {
    name: appServicePlanSku
    tier: appServicePlanSku == 'B1' || appServicePlanSku == 'B2' ? 'Basic' : 
          appServicePlanSku == 'S1' || appServicePlanSku == 'S2' ? 'Standard' : 'PremiumV2'
    capacity: environment == 'prod' ? 2 : 1
  }
  properties: {
    reserved: false // Windows
    zoneRedundant: environment == 'prod' ? true : false
  }
}

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: '${namePrefix}-sql${resourceNameSuffix}'
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlServerAdminLogin
    administratorLoginPassword: sqlServerAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: '${namePrefix}-db'
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'S1' : 'Basic'
    tier: environment == 'prod' ? 'Standard' : 'Basic'
    capacity: environment == 'prod' ? 20 : 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: environment == 'prod' ? 268435456000 : 2147483648 // 250GB for prod, 2GB for dev
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: environment == 'prod' ? true : false
    licenseType: 'LicenseIncluded'
    readScale: environment == 'prod' ? 'Enabled' : 'Disabled'
    requestedBackupStorageRedundancy: environment == 'prod' ? 'Geo' : 'Local'
  }
}

// Allow Azure services to access SQL Server
resource sqlServerFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (enableApplicationInsights) {
  name: '${namePrefix}-insights${resourceNameSuffix}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: environment == 'prod' ? 90 : 30
    SamplingPercentage: environment == 'prod' ? 100 : 50
    DisableIpMasking: false
  }
}

// Log Analytics Workspace (required for Application Insights)
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (enableApplicationInsights) {
  name: '${namePrefix}-logs${resourceNameSuffix}'
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

// Web App
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${namePrefix}-api${resourceNameSuffix}'
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      scmType: 'None'
      use32BitWorkerProcess: false
      alwaysOn: environment == 'prod' ? true : false
      managedPipelineMode: 'Integrated'
      virtualApplications: [
        {
          virtualPath: '/'
          physicalPath: 'site\\wwwroot'
          preloadEnabled: environment == 'prod' ? true : false
        }
      ]
      loadBalancing: 'LeastRequests'
      experiments: {
        rampUpRules: []
      }
      autoHealEnabled: environment == 'prod' ? true : false
      localMySqlEnabled: false
      ipSecurityRestrictions: [
        {
          ipAddress: 'Any'
          action: 'Allow'
          priority: 2147483647
          name: 'Allow all'
          description: 'Allow all access'
        }
      ]
      scmIpSecurityRestrictions: [
        {
          ipAddress: 'Any'
          action: 'Allow'
          priority: 2147483647
          name: 'Allow all'
          description: 'Allow all access'
        }
      ]
      scmIpSecurityRestrictionsUseMain: false
      http20Enabled: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'FtpsOnly'
      preWarmedInstanceCount: environment == 'prod' ? 1 : 0
      functionAppScaleLimit: 0
      functionsRuntimeScaleMonitoringEnabled: false
      minimumElasticInstanceCount: 0
      azureStorageAccounts: {}
    }
    scmSiteAlsoStopped: false
    clientAffinityEnabled: false
    clientCertEnabled: false
    clientCertMode: 'Required'
    hostNamesDisabled: false
    containerSize: 0
    dailyMemoryTimeQuota: 0
    redundancyMode: environment == 'prod' ? 'ActiveActive' : 'None'
    storageAccountRequired: false
    keyVaultReferenceIdentity: 'SystemAssigned'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Web App Staging Slot (Production only)
resource webAppStagingSlot 'Microsoft.Web/sites/slots@2023-01-01' = if (environment == 'prod') {
  parent: webApp
  name: 'staging'
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      scmType: 'None'
      use32BitWorkerProcess: false
      alwaysOn: true
      managedPipelineMode: 'Integrated'
    }
  }
}

// Web App Configuration
resource webAppConfig 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: webApp
  name: 'appsettings'
  properties: {
    WEBSITE_RUN_FROM_PACKAGE: '1'
    ASPNETCORE_ENVIRONMENT: environment == 'prod' ? 'Production' : 'Development'
    ConnectionStrings__DefaultConnection: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${sqlDatabase.name};User Id=${sqlServerAdminLogin};Password=${sqlServerAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
    APPINSIGHTS_INSTRUMENTATIONKEY: enableApplicationInsights ? applicationInsights.properties.InstrumentationKey : ''
    APPLICATIONINSIGHTS_CONNECTION_STRING: enableApplicationInsights ? applicationInsights.properties.ConnectionString : ''
    ApplicationInsightsAgent_EXTENSION_VERSION: '~3'
    XDT_MicrosoftApplicationInsights_Mode: 'Recommended'
    APPINSIGHTS_PROFILERFEATURE_VERSION: '1.0.0'
    APPINSIGHTS_SNAPSHOTFEATURE_VERSION: '1.0.0'
    InstrumentationEngine_EXTENSION_VERSION: 'disabled'
    SnapshotDebugger_EXTENSION_VERSION: 'disabled'
    XDT_MicrosoftApplicationInsights_BaseExtensions: 'disabled'
    DiagnosticServices_EXTENSION_VERSION: 'disabled'
    ANCM_ADDITIONAL_ERROR_PAGE_CONTENT_SEARCH_PATTERNS: 'System.InvalidOperationException'
    JwtSettings__Key: 'YourSuperSecretKeyThatIsAtLeast32CharactersLong!'
    JwtSettings__Issuer: 'CaseZeroAPI'
    JwtSettings__Audience: 'CaseZeroUsers'
    JwtSettings__ExpirationDays: '7'
    EmailSettings__SmtpServer: 'smtp.office365.com'
    EmailSettings__SmtpPort: '587'
    EmailSettings__UseSsl: 'true'
    EmailSettings__FromEmail: 'noreply@casezero.com'
    CasesBasePath: 'D:\\home\\site\\wwwroot\\cases'
  }
}

// Storage Account for backups and static content
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${namePrefix}stor${environment}${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    dnsEndpointType: 'Standard'
    defaultToOAuthAuthentication: false
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
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

// Static Web App for Frontend
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: '${namePrefix}-frontend${resourceNameSuffix}'
  location: 'Central US' // Static Web Apps have limited regions
  tags: tags
  sku: {
    name: environment == 'prod' ? 'Standard' : 'Free'
    tier: environment == 'prod' ? 'Standard' : 'Free'
  }
  properties: {
    repositoryUrl: 'https://github.com/lcarli/CaseZero-Alternative'
    branch: environment == 'prod' ? 'main' : 'develop'
    stagingEnvironmentPolicy: environment == 'prod' ? 'Enabled' : 'Disabled'
    allowConfigFileUpdates: true
    provider: 'GitHub'
    enterpriseGradeCdnStatus: environment == 'prod' ? 'Enabled' : 'Disabled'
  }
}

// Backup configuration for production
resource backupConfig 'Microsoft.Web/sites/config@2023-01-01' = if (enableBackup && environment == 'prod') {
  parent: webApp
  name: 'backup'
  properties: {
    backupSchedule: {
      frequencyInterval: 1
      frequencyUnit: 'Day'
      keepAtLeastOneBackup: true
      retentionPeriodInDays: 30
      startTime: '2023-01-01T02:00:00Z'
    }
    storageAccountUrl: enableBackup ? '${storageAccount.properties.primaryEndpoints.blob}backups' : ''
    databases: [
      {
        databaseType: 'SqlAzure'
        name: sqlDatabase.name
        connectionString: 'Server=${sqlServer.properties.fullyQualifiedDomainName};Database=${sqlDatabase.name};User Id=${sqlServerAdminLogin};Password=${sqlServerAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        connectionStringName: 'DefaultConnection'
      }
    ]
  }
}

// Outputs
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storageAccount.name
output applicationInsightsInstrumentationKey string = enableApplicationInsights ? applicationInsights.properties.InstrumentationKey : ''
output resourceGroupName string = resourceGroup().name