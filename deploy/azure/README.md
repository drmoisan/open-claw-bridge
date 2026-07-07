# OpenClaw Azure IaC (Stage 1 Footprint)

Bicep infrastructure-as-code that declaratively provisions the Stage 1 Azure footprint
for the existing, already-containerized `OpenClaw.Core` web host
(`deploy/docker/openclaw-core.Dockerfile`). See
`docs/features/active/2026-07-07-azure-bicep-iac-125/spec.md` for the full contract
(issue #125).

## Module Layout

```
deploy/azure/
  main.bicep                        # orchestrates the modules below, declares outputs
  modules/
    containerApp.bicep              # Container Apps Environment + Container App
    keyVault.bicep                  # Key Vault, enableRbacAuthorization: true
    queue.bicep                     # Service Bus namespace + queue
  parameters/
    main.dev.bicepparam             # using 'main.bicep' — dev environment values
```

## Structural Validation (in scope, automatable)

```
bicep build deploy/azure/main.bicep
```

Compiles `main.bicep` and all referenced modules to ARM JSON with zero errors. This
command runs both locally (where the Bicep CLI is available) and in CI via
`.github/workflows/_bicep-validate.yml`.

## Live Deployment (out of scope for automated execution)

```
az deployment group create --resource-group <rg> --template-file deploy/azure/main.bicep --parameters deploy/azure/parameters/main.dev.bicepparam
```

**This command is explicitly out of scope for automated execution in this feature.** No
Azure subscription or credentials exist in this workspace or in `ci.yml`. Live tenant
deployment is deferred to a human-executed runbook, authored separately (not by this
feature) as a `human_interaction` exception artifact, consistent with the F11/F14/F15/F17
precedent for tenant-dependent steps.
