// ==============================================================================
// Frontend Infrastructure - Main Template
// ==============================================================================
// Provisions CaseZero Frontend (React + Vite) as Static Web App
// ==============================================================================

targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
param environment string

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

// ==============================================================================
// Static Web App
// ==============================================================================
module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'frontend-static-web-app-deployment'
  params: {
    environment: environment
    namePrefix: namePrefix
    sku: staticWebAppSku
    backendApiUrl: backendApiUrl
    repositoryUrl: repositoryUrl
    branchName: branchName
    tags: tags
  }
}

// ==============================================================================
// Outputs
// ==============================================================================
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output staticWebAppId string = staticWebApp.outputs.staticWebAppId
output staticWebAppUrl string = staticWebApp.outputs.staticWebAppUrl
output staticWebAppDefaultHostname string = staticWebApp.outputs.staticWebAppDefaultHostname
