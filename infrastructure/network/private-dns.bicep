// ==============================================================================
// Private DNS Zones Module
// ==============================================================================
// Creates the private DNS zones required to resolve Storage and SQL private
// endpoints, and links them to every VNet that needs private resolution
// (canadacentral for the Function/storage, canadaeast for the API).
// ==============================================================================

targetScope = 'resourceGroup'

@description('Private DNS zone names to create (e.g. privatelink.blob.core.windows.net)')
param zoneNames array = [
  'privatelink.blob.${environment().suffixes.storage}'
  'privatelink.queue.${environment().suffixes.storage}'
  'privatelink.table.${environment().suffixes.storage}'
  'privatelink${az.environment().suffixes.sqlServerHostname}'
]

@description('VNet resource IDs to link the zones to: [{ name, id }]')
param vnetLinks array

@description('Resource tags')
param tags object = {}

// Flatten (zone x vnet) into a single list of link descriptors.
var linkPairs = flatten(map(zoneNames, zone => map(vnetLinks, v => {
  zone: zone
  vnetId: v.id
  linkName: 'link-${v.name}'
})))

resource zones 'Microsoft.Network/privateDnsZones@2020-06-01' = [for zone in zoneNames: {
  name: zone
  location: 'global'
  tags: tags
}]

resource links 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = [for pair in linkPairs: {
  name: '${pair.zone}/${pair.linkName}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: pair.vnetId
    }
  }
  dependsOn: [zones]
}]

@description('Map of zone name -> zone resourceId')
output zoneIds object = toObject(zoneNames, z => z, z => resourceId('Microsoft.Network/privateDnsZones', z))
