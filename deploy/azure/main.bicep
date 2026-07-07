// main.bicep — orchestrates the Stage 1 Azure footprint for OpenClaw.Core (F16, issue #125):
// a Container Apps environment + Container App hosting the existing openclaw-core image,
// an RBAC-scoped Key Vault, and a Service Bus namespace + queue for the future durable
// INotificationQueue backend. This is infrastructure authoring only; no runtime behavior
// change to OpenClaw.Core or OpenClaw.Core.CloudSync. See
// docs/features/active/2026-07-07-azure-bicep-iac-125/spec.md for the full contract.

@description('Logical environment label (e.g. dev, prod), used to derive resource names and tags.')
param environmentName string = 'dev'

@description('Azure region for all provisioned resources. Defaults to the resource group region.')
param location string = resourceGroup().location

@description('Naming prefix applied to all provisioned resource names.')
param resourceNamePrefix string = 'openclaw'

@description('Full reference (registry/repository:tag or @sha256 digest) to the openclaw-core container image. Required; no default — this value changes per build.')
param containerImage string

var tags = {
  environment: environmentName
  resourcePrefix: resourceNamePrefix
}

module containerAppModule 'modules/containerApp.bicep' = {
  name: '${resourceNamePrefix}-${environmentName}-containerApp-module'
  params: {
    containerAppEnvName: '${resourceNamePrefix}-${environmentName}-env'
    containerAppName: '${resourceNamePrefix}-${environmentName}-app'
    location: location
    containerImage: containerImage
    tags: tags
  }
}

module keyVaultModule 'modules/keyVault.bicep' = {
  name: '${resourceNamePrefix}-${environmentName}-keyVault-module'
  params: {
    keyVaultName: '${resourceNamePrefix}-${environmentName}-kv'
    location: location
    tags: tags
  }
}

module queueModule 'modules/queue.bicep' = {
  name: '${resourceNamePrefix}-${environmentName}-queue-module'
  params: {
    serviceBusNamespaceName: '${resourceNamePrefix}-${environmentName}-sbns'
    queueName: '${resourceNamePrefix}-${environmentName}-notifications'
    location: location
    tags: tags
  }
}

@description('The Container App public fully-qualified domain name.')
output containerAppFqdn string = containerAppModule.outputs.containerAppFqdn

@description('The Container App system-assigned managed identity principal ID.')
output containerAppPrincipalId string = containerAppModule.outputs.containerAppPrincipalId

@description('The Key Vault URI.')
output keyVaultUri string = keyVaultModule.outputs.keyVaultUri

@description('The Service Bus namespace fully-qualified host name (not a connection string).')
output serviceBusNamespaceEndpoint string = queueModule.outputs.serviceBusNamespaceEndpoint

@description('The name of the provisioned Service Bus queue.')
output serviceBusQueueName string = queueModule.outputs.serviceBusQueueName
