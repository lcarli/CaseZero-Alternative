// ==============================================================================
// Key Vault RBAC Assignment Module
// ==============================================================================
// This module assigns Key Vault Secrets User role to a principal
// Must be deployed in the same scope as the Key Vault
// ==============================================================================

targetScope = 'resourceGroup'

@description('Key Vault Resource ID')
param keyVaultId string

@description('Principal ID to grant access')
param principalId string

@description('Principal Type')
param principalType string = 'ServicePrincipal'

// Extract Key Vault name from resource ID
var keyVaultName = last(split(keyVaultId, '/'))

// Reference existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Assign Key Vault Secrets User role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: principalId
    principalType: principalType
  }
}

output roleAssignmentId string = roleAssignment.id
