// ==============================================================================
// Static Web App Module
// ==============================================================================
// Provisions Azure Static Web App for React Frontend
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Name prefix for resources')
param namePrefix string

@description('Static Web App SKU')
param sku object = {
  name: environment == 'prod' ? 'Standard' : 'Free'
  tier: environment == 'prod' ? 'Standard' : 'Free'
}

@description('Backend API URL')
param backendApiUrl string

@description('Repository URL (optional)')
param repositoryUrl string = ''

@description('Branch name (optional)')
param branchName string = environment == 'prod' ? 'main' : 'develop'

@description('Resource tags')
param tags object

var staticWebAppName = '${namePrefix}-web-${environment}'
var staticWebAppLocation = 'Central US' // Static Web Apps have limited regions

// Static Web App
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  tags: tags
  sku: sku
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

// Static Web App Config (App Settings)
resource staticWebAppConfig 'Microsoft.Web/staticSites/config@2023-01-01' = {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    VITE_API_BASE_URL: '${backendApiUrl}/api'
    VITE_APP_TITLE: 'CaseZero'
    VITE_ENV: environment
    NODE_ENV: environment == 'prod' ? 'production' : 'development'
  }
}

// Outputs
output staticWebAppName string = staticWebApp.name
output staticWebAppId string = staticWebApp.id
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
