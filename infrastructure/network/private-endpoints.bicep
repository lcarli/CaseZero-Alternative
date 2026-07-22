// ==============================================================================
// Private Endpoints Module
// ==============================================================================
// Creates private endpoints (with private DNS zone groups) for the Case
// Generator storage account (blob, queue, table) and, optionally, the Azure SQL
// server. Deployed into the resource group hosting the PE subnet.
// ==============================================================================

targetScope = 'resourceGroup'

@description('Location for the private endpoints')
param location string

@description('Resource ID of the subnet that hosts the private endpoints')
param subnetId string

@description('Storage Account resource ID')
param storageAccountId string

@description('Storage sub-resources (group IDs) to create endpoints for')
param storageGroupIds array = [
  'blob'
  'queue'
  'table'
]

@description('Azure SQL Server resource ID (empty to skip the SQL private endpoint)')
param sqlServerId string = ''

@description('Map of private DNS zone name -> zone resourceId')
param dnsZoneIds object

@description('Resource tags')
param tags object = {}

var storageZoneSuffix = {
  blob: 'privatelink.blob.${environment().suffixes.storage}'
  queue: 'privatelink.queue.${environment().suffixes.storage}'
  table: 'privatelink.table.${environment().suffixes.storage}'
}
var sqlZoneName = 'privatelink${az.environment().suffixes.sqlServerHostname}'

// ---------------------------------------------------------------------------
// Storage private endpoints (one per sub-resource)
// ---------------------------------------------------------------------------
resource storagePe 'Microsoft.Network/privateEndpoints@2023-11-01' = [for groupId in storageGroupIds: {
  name: 'pe-st-${groupId}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: subnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'conn-${groupId}'
        properties: {
          privateLinkServiceId: storageAccountId
          groupIds: [groupId]
        }
      }
    ]
  }
}]

resource storageDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = [for (groupId, i) in storageGroupIds: {
  parent: storagePe[i]
  name: 'zg-${groupId}'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: groupId
        properties: {
          privateDnsZoneId: dnsZoneIds[storageZoneSuffix[groupId]]
        }
      }
    ]
  }
}]

// ---------------------------------------------------------------------------
// SQL private endpoint (optional)
// ---------------------------------------------------------------------------
resource sqlPe 'Microsoft.Network/privateEndpoints@2023-11-01' = if (!empty(sqlServerId)) {
  name: 'pe-sql'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: subnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'conn-sql'
        properties: {
          privateLinkServiceId: sqlServerId
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

resource sqlDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = if (!empty(sqlServerId)) {
  parent: sqlPe
  name: 'zg-sql'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql'
        properties: {
          privateDnsZoneId: dnsZoneIds[sqlZoneName]
        }
      }
    ]
  }
}
