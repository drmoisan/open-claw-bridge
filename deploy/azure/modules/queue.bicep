// queue.bicep — Azure Service Bus namespace + queue for the durable INotificationQueue
// backend (F16, issue #125). Provisions the resource only; the F14 in-process
// ChannelNotificationQueue default is unchanged and no Azure SDK dependency is
// added to OpenClaw.Core/OpenClaw.Core.CloudSync by this module. Per this
// feature's research artifact Decision 3, Standard SKU is used and no
// connection-string value is ever output — only the namespace host name and
// the queue name, both non-secret identifiers.

@description('Name of the Service Bus namespace.')
param serviceBusNamespaceName string

@description('Name of the Service Bus queue provisioned inside the namespace.')
param queueName string

@description('Azure region for the Service Bus namespace.')
param location string

@description('Tags applied to the Service Bus namespace.')
param tags object = {}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: queueName
}

@description('The Service Bus namespace fully-qualified host name (not a connection string), e.g. <name>.servicebus.windows.net.')
output serviceBusNamespaceEndpoint string = serviceBusNamespace.properties.serviceBusEndpoint

@description('The name of the provisioned Service Bus queue.')
output serviceBusQueueName string = serviceBusQueue.name
