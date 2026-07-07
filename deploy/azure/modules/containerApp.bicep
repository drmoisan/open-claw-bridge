// containerApp.bicep — Azure Container Apps Environment + Container App hosting the
// existing openclaw-core image (F16, issue #125). Per this feature's research
// artifact Decision 1, Container Apps is favored because OpenClaw.Core is already
// a persistent, containerized ASP.NET Core host (deploy/docker/openclaw-core.Dockerfile,
// EXPOSE 8081), not a Functions-triggered workload. The Container App uses a
// system-assigned managed identity so a future feature can grant it least-privilege
// RBAC roles against the Key Vault and Service Bus modules without any credential
// ever being inlined here.

@description('Name of the Container Apps managed environment.')
param containerAppEnvName string

@description('Name of the Container App.')
param containerAppName string

@description('Azure region for the Container Apps environment and Container App.')
param location string

@description('Full reference (registry/repository:tag or @sha256 digest) to the openclaw-core container image. No default: this value changes per build and must be supplied explicitly.')
param containerImage string

@description('Tags applied to the Container Apps resources.')
param tags object = {}

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppEnvName
  location: location
  tags: tags
  properties: {}
}

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8081
        transport: 'auto'
      }
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: containerImage
        }
      ]
    }
  }
}

@description('The public fully-qualified domain name of the Container App.')
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn

@description('The system-assigned managed identity principal ID, for a future Key Vault / Service Bus role assignment.')
output containerAppPrincipalId string = containerApp.identity.principalId
