// ==============================================================================
// RBAC Assignment Module for Key Vault
// ==============================================================================
// Assigns Key Vault roles to managed identities
// ==============================================================================

@description('Key Vault name')
param keyVaultName string

@description('Principal ID (Managed Identity)')
param principalId string

@description('Role Definition ID (GUID)')
param roleDefinitionId string

// Reference to existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// RBAC Assignment
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, roleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output roleAssignmentId string = roleAssignment.id
