// ==============================================================================
// Virtual Network Module
// ==============================================================================
// Creates a single VNet with a configurable set of subnets. Subnets may include
// a service delegation (used for App Service / Function VNet integration) and a
// flag to disable private-endpoint network policies (used for the PE subnet).
// ==============================================================================

targetScope = 'resourceGroup'

@description('VNet name')
param name string

@description('Location')
param location string

@description('Address space (single CIDR)')
param addressPrefix string

@description('Subnets: [{ name, prefix, delegation (optional), disablePeNetworkPolicies (optional bool) }]')
param subnets array

@description('Resource tags')
param tags object = {}

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [addressPrefix]
    }
    subnets: [for subnet in subnets: {
      name: subnet.name
      properties: {
        addressPrefixes: [subnet.prefix]
        privateEndpointNetworkPolicies: (subnet.?disablePeNetworkPolicies ?? false) ? 'Disabled' : 'Enabled'
        delegations: empty(subnet.?delegation ?? '') ? [] : [
          {
            name: 'delegation'
            properties: {
              serviceName: subnet.delegation
            }
          }
        ]
      }
    }]
  }
}

output id string = vnet.id
output name string = vnet.name
@description('Map of subnet name -> subnet resourceId')
output subnetIds object = toObject(subnets, s => s.name, s => resourceId('Microsoft.Network/virtualNetworks/subnets', name, s.name))
