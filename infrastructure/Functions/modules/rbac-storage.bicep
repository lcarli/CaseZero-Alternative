// ==============================================================================
// RBAC Assignment Module for Storage Account
// ==============================================================================
// Assigns Storage roles to managed identities
// ==============================================================================

@description('Storage Account name')
param storageAccountName string

@description('Principal ID (Managed Identity)')
param principalId string

@description('Role Definition ID (GUID)')
param roleDefinitionId string

// Reference to existing Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

// RBAC Assignment
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, principalId, roleDefinitionId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output roleAssignmentId string = roleAssignment.id
