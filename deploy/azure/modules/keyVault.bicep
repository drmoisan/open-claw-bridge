// keyVault.bicep — Azure Key Vault for the OpenClaw Stage 1 Azure footprint (F16, issue #125).
//
// Provisions a Key Vault with Azure RBAC authorization (not the legacy access-policy
// model), per this feature's research artifact Decision 2. No secret value is
// declared, referenced, or set anywhere in this module; the vault is provisioned
// empty and secrets are populated out-of-band at deploy time or by a separate
// operational process.

@description('Name of the Key Vault resource.')
param keyVaultName string

@description('Azure region for the Key Vault resource.')
param location string

@description('Tags applied to the Key Vault resource.')
param tags object = {}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

@description('The Key Vault URI, e.g. https://<name>.vault.azure.net/.')
output keyVaultUri string = keyVault.properties.vaultUri

@description('The Key Vault resource ID, for role-assignment wiring by a future feature.')
output keyVaultResourceId string = keyVault.id

@description('The Key Vault resource name.')
output keyVaultName string = keyVault.name
