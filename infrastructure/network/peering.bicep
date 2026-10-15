// ==============================================================================
// VNet Peering Module
// ==============================================================================
// Creates a one-directional peering from an existing local VNet to a remote
// VNet. Deploy once per direction (in each VNet's resource group).
// ==============================================================================

targetScope = 'resourceGroup'

@description('Name of the peering resource')
param name string

@description('Name of the existing local VNet the peering is attached to')
param localVnetName string

@description('Resource ID of the remote VNet to peer with')
param remoteVnetId string

resource localVnet 'Microsoft.Network/virtualNetworks@2023-11-01' existing = {
  name: localVnetName
}

resource peering 'Microsoft.Network/virtualNetworks/virtualNetworkPeerings@2023-11-01' = {
  parent: localVnet
  name: name
  properties: {
    remoteVirtualNetwork: {
      id: remoteVnetId
    }
    allowVirtualNetworkAccess: true
    allowForwardedTraffic: false
    allowGatewayTransit: false
    useRemoteGateways: false
  }
}
