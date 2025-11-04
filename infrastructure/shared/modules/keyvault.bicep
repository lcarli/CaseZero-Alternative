// ==============================================================================
// Key Vault Module
// ==============================================================================
// Provisions Azure Key Vault for secrets management
// ==============================================================================

@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('Name prefix for resources')
param namePrefix string

@description('Enable RBAC authorization')
param enableRbacAuthorization bool = true

@description('Soft delete retention in days')
param softDeleteRetentionInDays int = environment == 'prod' ? 90 : 7

@description('Resource tags')
param tags object

var keyVaultName = '${namePrefix}-kv-${environment}-${uniqueString(resourceGroup().id)}'

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
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
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enableRbacAuthorization: enableRbacAuthorization
    enablePurgeProtection: environment == 'prod' ? true : null
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// Outputs
output keyVaultName string = keyVault.name
output keyVaultId string = keyVault.id
output keyVaultUri string = keyVault.properties.vaultUri
