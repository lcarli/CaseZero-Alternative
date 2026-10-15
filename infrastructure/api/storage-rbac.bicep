// ==============================================================================
// Storage RBAC Assignment Module
// ==============================================================================
// Assigns Storage data-plane roles to a principal at the scope of an existing
// Storage Account. Deploy this module at the resource group where the storage
// account lives (cross-RG scope is supported from a caller in another RG).
// ==============================================================================

targetScope = 'resourceGroup'

@description('Storage Account name (in the resource group this module is deployed to)')
param storageAccountName string

@description('Principal ID to grant access')
param principalId string

@description('Principal Type (ServicePrincipal for managed identities)')
param principalType string = 'ServicePrincipal'

@description('Assign Storage Blob Data Reader role')
param assignBlobReader bool = true

@description('Assign Storage Blob Data Contributor role (broader than Reader)')
param assignBlobContributor bool = false

@description('Assign Storage Queue Data Message Sender role (send-only)')
param assignQueueMessageSender bool = true

@description('Assign Storage Queue Data Contributor role (queue management + messages)')
param assignQueueContributor bool = false

// Built-in role IDs (data plane). Source: Azure built-in roles documentation.
var roleIds = {
  blobDataReader: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
  blobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  queueDataMessageSender: 'c6a89b2d-59bc-44d0-9896-0f6e12d7b80a'
  queueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (assignBlobReader) {
  name: guid(storageAccount.id, principalId, roleIds.blobDataReader)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.blobDataReader)
    principalId: principalId
    principalType: principalType
  }
}

resource blobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (assignBlobContributor) {
  name: guid(storageAccount.id, principalId, roleIds.blobDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.blobDataContributor)
    principalId: principalId
    principalType: principalType
  }
}

resource queueMessageSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (assignQueueMessageSender) {
  name: guid(storageAccount.id, principalId, roleIds.queueDataMessageSender)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.queueDataMessageSender)
    principalId: principalId
    principalType: principalType
  }
}

resource queueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (assignQueueContributor) {
  name: guid(storageAccount.id, principalId, roleIds.queueDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.queueDataContributor)
    principalId: principalId
    principalType: principalType
  }
}
