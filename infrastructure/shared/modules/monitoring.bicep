// ==============================================================================
// Monitoring Module
// ==============================================================================
// Provisions Log Analytics Workspace and Application Insights
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('Name prefix for resources')
param namePrefix string

@description('Enable Application Insights')
param enableApplicationInsights bool = true

@description('Data retention in days')
param retentionInDays int = environment == 'prod' ? 90 : 30

@description('Sampling percentage for telemetry')
param samplingPercentage int = environment == 'prod' ? 100 : 50

@description('Resource tags')
param tags object

var logAnalyticsName = '${namePrefix}-logs-${environment}'
var appInsightsName = '${namePrefix}-insights-${environment}'

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (enableApplicationInsights) {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      searchVersion: 1
      legacy: 0
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (enableApplicationInsights) {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: enableApplicationInsights ? logAnalyticsWorkspace.id : null
    RetentionInDays: retentionInDays
    SamplingPercentage: samplingPercentage
    DisableIpMasking: false
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Outputs
output logAnalyticsWorkspaceName string = enableApplicationInsights ? logAnalyticsWorkspace.name : ''
output logAnalyticsWorkspaceId string = enableApplicationInsights ? logAnalyticsWorkspace.id : ''
output applicationInsightsName string = enableApplicationInsights ? applicationInsights.name : ''
output applicationInsightsId string = enableApplicationInsights ? applicationInsights.id : ''
output instrumentationKey string = enableApplicationInsights ? (applicationInsights.?properties.InstrumentationKey ?? '') : ''
output connectionString string = enableApplicationInsights ? (applicationInsights.?properties.ConnectionString ?? '') : ''
