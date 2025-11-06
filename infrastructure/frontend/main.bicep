// ==============================================================================
// Frontend Infrastructure - Main Template (Native Azure Resources)
// ==============================================================================
// Provisions CaseZero Frontend (React + Vite) as Static Web App
// Note: Azure Static Web Apps don't have an AVM module yet, using native resource
// Reference: https://learn.microsoft.com/en-us/azure/templates/microsoft.web/staticsites
// ==============================================================================

targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources (Static Web Apps have limited regions)')
param location string = 'Central US'

@description('Name prefix for resources')
param namePrefix string = 'casezero'

@description('Static Web App SKU')
param staticWebAppSku object = {
  name: environment == 'prod' ? 'Standard' : 'Free'
  tier: environment == 'prod' ? 'Standard' : 'Free'
}

@description('Backend API URL')
param backendApiUrl string

@description('GitHub Repository URL (optional)')
param repositoryUrl string = ''

@description('GitHub Branch name')
param branchName string = environment == 'prod' ? 'main' : 'develop'

var tags = {
  Environment: environment
  Project: 'CaseZero'
  ManagedBy: 'Bicep-IaC'
  DeployedBy: 'GitHub-Actions'
  Layer: 'Frontend'
  Component: 'StaticWebApp'
  Framework: 'react-vite'
}

var staticWebAppName = '${namePrefix}-web-${environment}'

// ==============================================================================
// Static Web App (Native Azure Resource)
// ==============================================================================
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  tags: tags
  sku: staticWebAppSku
  properties: {
    repositoryUrl: !empty(repositoryUrl) ? repositoryUrl : null
    branch: !empty(repositoryUrl) ? branchName : null
    stagingEnvironmentPolicy: environment == 'prod' ? 'Enabled' : 'Disabled'
    allowConfigFileUpdates: true
    provider: !empty(repositoryUrl) ? 'GitHub' : 'None'
    enterpriseGradeCdnStatus: environment == 'prod' ? 'Enabled' : 'Disabled'
    buildProperties: {
      appLocation: '/frontend'
      apiLocation: ''
      outputLocation: 'dist'
      appBuildCommand: 'npm run build'
      apiBuildCommand: ''
      skipGithubActionWorkflowGeneration: true
    }
  }
}

// ==============================================================================
// Static Web App Configuration (App Settings)
// ==============================================================================
resource staticWebAppConfig 'Microsoft.Web/staticSites/config@2023-12-01' = {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    VITE_API_BASE_URL: '${backendApiUrl}/api'
    VITE_APP_TITLE: 'CaseZero'
    VITE_ENV: environment
    NODE_ENV: environment == 'prod' ? 'production' : 'development'
  }
}

// ==============================================================================
// Outputs
// ==============================================================================
output staticWebAppName string = staticWebApp.name
output staticWebAppId string = staticWebApp.id
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
