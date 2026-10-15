// ==============================================================================
// Main Orchestrator - 3-Tier Architecture (Azure Verified Modules)
// ==============================================================================
// Orchestrates deployment of all CaseZero infrastructure layers using 100% AVM:
// - Shared: Key Vault, Log Analytics, App Insights, SQL (All AVM 0.9-0.20)
// - API: Backend API (.NET 8.0) - App Service Plan + App Service (AVM 0.4-0.14)
// - Functions: Case Generator (.NET 9.0) - Storage, Plan, Functions (AVM 0.4-0.17)
// - Frontend: Static Web App (React + Vite) - Native Azure Resource
// Reference: https://azure.github.io/Azure-Verified-Modules/
// ==============================================================================

targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string = 'canadacentral'

@description('Location for the API layer (App Service). Defaults to the primary location; set to the API region when it differs, e.g. canadaeast.')
param apiLocation string = location

@description('Name prefix for all resources')
param namePrefix string = 'casezero'

@description('Enable SQL Database (optional)')
param enableSqlDatabase bool = false

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string = ''

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string = ''

@description('Enable monitoring (Application Insights + Log Analytics)')
param enableMonitoring bool = true

@description('GitHub Repository URL for Static Web App (optional)')
param repositoryUrl string = ''

@description('GitHub Branch name')
param branchName string = environment == 'prod' ? 'main' : 'develop'

@description('Enable private networking (VNets, peering, private DNS and private endpoints for storage + SQL)')
param enablePrivateNetworking bool = true

@description('Address space for the canadacentral VNet (storage + Function)')
param ccVnetAddressPrefix string = '10.30.0.0/16'

@description('Address space for the canadaeast VNet (API)')
param ceVnetAddressPrefix string = '10.31.0.0/16'

// ==============================================================================
// Resource Groups
// ==============================================================================

// Shared Resource Group
resource sharedResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-shared-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'Shared'
    ManagedBy: 'Bicep-IaC'
  }
}

// API Resource Group
resource apiResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-api-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'API'
    ManagedBy: 'Bicep-IaC'
  }
}

// Functions Resource Group
resource functionsResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-func-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'Functions'
    ManagedBy: 'Bicep-IaC'
  }
}

// Frontend Resource Group
resource frontendResourceGroup 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: '${namePrefix}-web-${environment}-rg'
  location: location
  tags: {
    Environment: environment
    Project: 'CaseZero'
    Layer: 'Frontend'
    ManagedBy: 'Bicep-IaC'
  }
}

// ==============================================================================
// Layer 0: Private Networking (VNets, peering, private DNS, private endpoints)
// ==============================================================================
// canadacentral VNet: hosts the storage/SQL private endpoints and the Function
// App regional VNet integration. canadaeast VNet: hosts the API App Service
// regional VNet integration. The two are peered so the API can reach the
// private endpoints in canadacentral.
var ccVnetName = 'vnet-${namePrefix}-cc-${environment}'
var ceVnetName = 'vnet-${namePrefix}-ce-${environment}'

module ccVnet 'network/vnet.bicep' = if (enablePrivateNetworking) {
  name: 'cc-vnet-deployment'
  scope: functionsResourceGroup
  params: {
    name: ccVnetName
    location: location
    addressPrefix: ccVnetAddressPrefix
    subnets: [
      {
        name: 'snet-pe'
        prefix: cidrSubnet(ccVnetAddressPrefix, 24, 1)
        disablePeNetworkPolicies: true
      }
      {
        name: 'snet-func-integration'
        prefix: cidrSubnet(ccVnetAddressPrefix, 24, 2)
        delegation: 'Microsoft.App/environments'
      }
    ]
  }
}

module ceVnet 'network/vnet.bicep' = if (enablePrivateNetworking) {
  name: 'ce-vnet-deployment'
  scope: apiResourceGroup
  params: {
    name: ceVnetName
    location: apiLocation
    addressPrefix: ceVnetAddressPrefix
    subnets: [
      {
        name: 'snet-api-integration'
        prefix: cidrSubnet(ceVnetAddressPrefix, 24, 1)
        delegation: 'Microsoft.Web/serverFarms'
      }
    ]
  }
}

module peeringCcToCe 'network/peering.bicep' = if (enablePrivateNetworking) {
  name: 'peering-cc-to-ce-deployment'
  scope: functionsResourceGroup
  params: {
    name: 'cc-to-ce'
    localVnetName: ccVnetName
    remoteVnetId: ceVnet.outputs.id
  }
}

module peeringCeToCc 'network/peering.bicep' = if (enablePrivateNetworking) {
  name: 'peering-ce-to-cc-deployment'
  scope: apiResourceGroup
  params: {
    name: 'ce-to-cc'
    localVnetName: ceVnetName
    remoteVnetId: ccVnet.outputs.id
  }
}

module privateDns 'network/private-dns.bicep' = if (enablePrivateNetworking) {
  name: 'private-dns-deployment'
  scope: functionsResourceGroup
  params: {
    vnetLinks: [
      {
        name: 'cc'
        id: ccVnet.outputs.id
      }
      {
        name: 'ce'
        id: ceVnet.outputs.id
      }
    ]
  }
}

// ==============================================================================
// Layer 1: Shared Infrastructure
// ==============================================================================
module sharedInfrastructure 'shared/main.bicep' = {
  name: 'shared-infrastructure-deployment'
  scope: sharedResourceGroup
  params: {
    environment: environment
    location: location
    namePrefix: namePrefix
    enableSqlDatabase: enableSqlDatabase
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    enableMonitoring: enableMonitoring
  }
}

// ==============================================================================
// Layer 2: API Backend Infrastructure
// ==============================================================================
// NOTE: this layer depends on the functions layer outputs (storage account +
// function URL) so the API can talk to the Case Generator using managed
// identity. The implicit dependency forces api to deploy after functions.
module apiInfrastructure 'api/main.bicep' = {
  name: 'api-infrastructure-deployment'
  scope: apiResourceGroup
  params: {
    environment: environment
    location: apiLocation
    namePrefix: namePrefix
    appServicePlanSku: {
      name: environment == 'prod' ? 'P1v3' : 'B1'
      tier: environment == 'prod' ? 'PremiumV3' : 'Basic'
      capacity: environment == 'prod' ? 2 : 1
    }
    useSqlite: !enableSqlDatabase
    sqlConnectionString: enableSqlDatabase ? sharedInfrastructure.outputs.sqlConnectionString : ''
    keyVaultUri: sharedInfrastructure.outputs.keyVaultUri
    keyVaultId: sharedInfrastructure.outputs.keyVaultId
    appInsightsConnectionString: enableMonitoring ? sharedInfrastructure.outputs.connectionString : ''
    appInsightsInstrumentationKey: enableMonitoring ? sharedInfrastructure.outputs.instrumentationKey : ''
    // corsAllowedOrigins intentionally not set here — the api/main.bicep default
    // covers localhost dev. The deployed frontend origin (Static Web App auto
    // hostname) is not known at this point in the deployment (api deploys
    // before frontend to avoid a circular dependency on backendApiUrl), so it
    // must be added post-provision via:
    //   az webapp config appsettings set -n <api> -g <rg> \
    //       --settings Cors__AllowedOrigins__2=https://<swa-hostname>
    // Prod: pass corsAllowedOrigins explicitly with the custom domain.
    caseGeneratorFunctionBaseUrl: functionsInfrastructure.outputs.functionAppUrl
    caseGeneratorStorageAccountName: functionsInfrastructure.outputs.storageAccountName
    caseGeneratorStorageAccountId: functionsInfrastructure.outputs.storageAccountId
    apiVnetSubnetId: enablePrivateNetworking ? ceVnet.outputs.subnetIds['snet-api-integration'] : ''
  }
}

// ==============================================================================
// Layer 3: Functions Infrastructure
// ==============================================================================
module functionsInfrastructure 'functions/main.bicep' = {
  name: 'functions-infrastructure-deployment'
  scope: functionsResourceGroup
  params: {
    environment: environment
    location: location
    namePrefix: 'casegen'
    functionAppPlanSku: {
      name: environment == 'prod' ? 'EP1' : 'Y1'
      tier: environment == 'prod' ? 'ElasticPremium' : 'Dynamic'
    }
    storageSku: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'
    containerNames: [
      'cases'
      'bundles'
      'case-context'
      'logs'
    ]
    keyVaultUri: sharedInfrastructure.outputs.keyVaultUri
    keyVaultId: sharedInfrastructure.outputs.keyVaultId
    appInsightsConnectionString: enableMonitoring ? sharedInfrastructure.outputs.connectionString : ''
    appInsightsInstrumentationKey: enableMonitoring ? sharedInfrastructure.outputs.instrumentationKey : ''
    functionVnetSubnetId: enablePrivateNetworking ? ccVnet.outputs.subnetIds['snet-func-integration'] : ''
  }
}

// ==============================================================================
// Layer 5: Private Endpoints (storage + SQL) — after storage/SQL exist
// ==============================================================================
module privateEndpoints 'network/private-endpoints.bicep' = if (enablePrivateNetworking) {
  name: 'private-endpoints-deployment'
  scope: functionsResourceGroup
  params: {
    location: location
    subnetId: ccVnet.outputs.subnetIds['snet-pe']
    storageAccountId: functionsInfrastructure.outputs.storageAccountId
    sqlServerId: enableSqlDatabase ? sharedInfrastructure.outputs.sqlServerId : ''
    dnsZoneIds: privateDns.outputs.zoneIds
  }
}

// ==============================================================================
// Layer 4: Frontend Infrastructure
// ==============================================================================
module frontendInfrastructure 'frontend/main.bicep' = {
  name: 'frontend-infrastructure-deployment'
  scope: frontendResourceGroup
  params: {
    environment: environment
    namePrefix: namePrefix
    staticWebAppSku: {
      name: environment == 'prod' ? 'Standard' : 'Free'
      tier: environment == 'prod' ? 'Standard' : 'Free'
    }
    backendApiUrl: apiInfrastructure.outputs.apiAppServiceUrl
    repositoryUrl: repositoryUrl
    branchName: branchName
  }
}

// ==============================================================================
// RBAC Assignments
// ==============================================================================
// Note: RBAC assignments managed by AVM modules automatically via managedIdentities parameter
// The API and Functions apps use System-Assigned Managed Identity
// Key Vault access is granted through RBAC authorization (enableRbacAuthorization: true)
// Storage access for Functions is granted through the AVM storage module

// ==============================================================================
// Outputs
// ==============================================================================

// Resource Group outputs
output sharedResourceGroupName string = sharedResourceGroup.name
output apiResourceGroupName string = apiResourceGroup.name
output functionsResourceGroupName string = functionsResourceGroup.name
output frontendResourceGroupName string = frontendResourceGroup.name

// Shared infrastructure outputs
output keyVaultName string = sharedInfrastructure.outputs.keyVaultName
output keyVaultUri string = sharedInfrastructure.outputs.keyVaultUri
output applicationInsightsName string = enableMonitoring ? sharedInfrastructure.outputs.applicationInsightsName : ''
output logAnalyticsWorkspaceName string = enableMonitoring ? sharedInfrastructure.outputs.logAnalyticsWorkspaceName : ''
output sqlServerName string = enableSqlDatabase ? sharedInfrastructure.outputs.sqlServerName : ''
output sqlServerFqdn string = enableSqlDatabase ? sharedInfrastructure.outputs.sqlServerFqdn : ''
output sqlDatabaseName string = enableSqlDatabase ? sharedInfrastructure.outputs.sqlDatabaseName : ''

// API infrastructure outputs
output apiAppServiceName string = apiInfrastructure.outputs.apiAppServiceName
output apiAppServiceUrl string = apiInfrastructure.outputs.apiAppServiceUrl

// Functions infrastructure outputs
output functionAppName string = functionsInfrastructure.outputs.functionAppName
output functionAppUrl string = functionsInfrastructure.outputs.functionAppUrl
output functionsStorageAccountName string = functionsInfrastructure.outputs.storageAccountName

// Frontend infrastructure outputs
output staticWebAppName string = frontendInfrastructure.outputs.staticWebAppName
output staticWebAppUrl string = frontendInfrastructure.outputs.staticWebAppUrl

// Summary
output deploymentSummary object = {
  environment: environment
  location: location
  frontendUrl: frontendInfrastructure.outputs.staticWebAppUrl
  apiUrl: apiInfrastructure.outputs.apiAppServiceUrl
  functionsUrl: functionsInfrastructure.outputs.functionAppUrl
  sqlEnabled: enableSqlDatabase
  monitoringEnabled: enableMonitoring
}
