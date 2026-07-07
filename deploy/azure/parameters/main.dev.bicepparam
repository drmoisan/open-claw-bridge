// main.dev.bicepparam — dev-environment parameter binding for deploy/azure/main.bicep.
// No secret, credential, or connection-string value is set here. `containerImage`
// is a documented placeholder: no image has been pushed to a registry from this
// workspace, so the real value must be supplied/overridden at deploy time
// (e.g. `az deployment group create ... --parameters containerImage=<real-image-ref>`).
using 'main.bicep'

param environmentName = 'dev'

// Placeholder only — not a real registry reference or secret. Override at deploy time.
param containerImage = 'REPLACE_AT_DEPLOY_TIME/openclaw-core:unset'
