# Azure Bicep IaC Deployment — Human-Exception Runbook

- **Feature:** azure-bicep-iac (Issue #125)
- **Runbook path:** `docs/features/active/2026-07-07-azure-bicep-iac-125/runbooks/azure-bicep-deployment.runbook.md`
- **Authored:** 2026-07-07
- **Source artifact:** `docs/features/active/2026-07-07-azure-bicep-iac-125/research/2026-07-07-bicep-iac-architecture.md` (architecture decisions this runbook operationalizes)

## Cue

Act on this runbook when both of the following are true:

1. This feature's pull request (Bicep IaC for issue #125: `deploy/azure/main.bicep`, `deploy/azure/modules/{containerApp.bicep, keyVault.bicep, queue.bicep}`, `deploy/azure/parameters/main.dev.bicepparam`) has merged, and CI's `_bicep-validate.yml` reusable workflow has reported a passing structural check (`bicep build`/`bicep lint`) against the merged branch head.
2. The live deployment has not yet happened in the target environment, and it must complete **before** the `OpenClaw:CloudSync:Enabled` flag (introduced by F14, `2026-07-03-graph-subscriptions-delta-117`) is turned on in that environment — enabling that flag without the Container App, Key Vault, and Service Bus queue already provisioned would activate a webhook endpoint and queue seam with no reachable Azure hosting target.

Live execution of `az deployment group create` (or `az deployment sub create`) against a real Azure tenant, and the post-deployment verification that follows it, cannot run in this repository's CI or in this local workspace: no Azure subscription or credentials are configured in `.github/workflows/ci.yml` (no `azure/login` step, no Azure secret reference) or in this workspace. This is classified Automatable-with-credentials in the research artifact (`research/2026-07-07-bicep-iac-architecture.md` §"Automation Feasibility"), resolved as a permitted `human_interaction` exception, and this runbook is the human follow-up, following the F11 (`exchange-rbac-scripts-111`) precedent for a scripts-plus-runbook deliverable.

## Prerequisites

All of the following must be true before starting:

1. **Azure subscription and resource group.** An Azure subscription is available, and a target resource group exists (or will be created) to deploy into. Record the subscription ID and resource group name for use in the commands below.
2. **Signed-in `az` session with sufficient rights.** The executing operator has **Contributor** (or an equivalent least-privilege custom role covering Container Apps, Key Vault, and Service Bus resource creation) on the target resource group. The Azure CLI (`az`) is installed and the operator can run `az login` interactively or with a service principal.
3. **Container image already published.** The `openclaw-core` container image (built from `deploy/docker/openclaw-core.Dockerfile`) has already been pushed to a container registry that the deployment identity (and, later, the Container App's managed identity, if the registry requires authenticated pulls) can reach. This feature does not build or push that image; building and pushing it is a prerequisite performed outside this runbook's scope.
4. **Bicep tooling.** Either the standalone Bicep CLI or the Azure CLI with the Bicep extension installed (`az bicep version` returns a version, or `az bicep install` has been run). Bicep CLI v0.22.x or later and Azure CLI v2.53.0 or later are required to deploy directly from a `.bicepparam` file without a separate `--template-file` switch.
5. **Repository checkout.** A checkout of this repository at (or after) the merged commit containing `deploy/azure/main.bicep`, its modules, and `deploy/azure/parameters/main.dev.bicepparam`.
6. **Real container image reference in hand.** The full registry/repository:tag (or `@sha256:` digest) reference for the published `openclaw-core` image, to override the template's required `containerImage` parameter at deploy time (the dev parameter file intentionally sets no default for this parameter).

## Step-by-step Instructions

**All step-by-step instructions in this runbook are Azure CLI / Bicep CLI steps. No third-party UI navigation is required.**

Replace every placeholder value (`<subscription-id>`, `<rg>`, `<real-image-reference>`) with real values before running each command live.

### Step 1 — Sign in and select the subscription

```powershell
az login
az account set --subscription '<subscription-id>'
```

### Step 2 — Confirm the target resource group

```powershell
az group show --name '<rg>' --output table
```

If the resource group does not yet exist, create it first:

```powershell
az group create --name '<rg>' --location '<azure-region>'
```

### Step 3 — Final pre-flight compile check

Run a local `bicep build` as a last structural check before invoking a live deployment, even though CI's `_bicep-validate.yml` has already validated the merged branch head:

```powershell
bicep build deploy/azure/main.bicep
```

This compiles `main.bicep` and all three referenced modules (`containerApp.bicep`, `keyVault.bicep`, `queue.bicep`) to ARM JSON. A non-zero exit or any printed error means the template does not compile; stop and do not proceed to Step 4 until this step reports success with no errors.

### Step 4 — Run the live deployment, overriding the placeholder `containerImage` value

The committed `deploy/azure/parameters/main.dev.bicepparam` file sets `environmentName = 'dev'` but intentionally leaves `containerImage` unset (no dev-time default exists, since no image is pushed from this workspace). Supply the real image reference as a command-line override, which takes precedence over the parameter file per the documented `--parameters` evaluation order (later values win when a parameter is assigned more than once):

```powershell
az deployment group create `
    --resource-group '<rg>' `
    --template-file deploy/azure/main.bicep `
    --parameters deploy/azure/parameters/main.dev.bicepparam `
    --parameters containerImage='<real-image-reference>'
```

If preferred, run `az deployment group what-if` with the same arguments first to review the predicted resource changes before committing to the live run.

### Step 5 — Capture the deployment outputs

`main.bicep` declares five outputs. Capture all of them for downstream use (future features wiring `OpenClaw.Core.CloudSync` to the durable queue, or the F12/F13 auth module to Key Vault):

```powershell
az deployment group show `
    --resource-group '<rg>' `
    --name main `
    --query properties.outputs `
    --output json
```

Record the following fields from the output:

- `containerAppFqdn` — the Container App's public fully-qualified domain name.
- `containerAppPrincipalId` — the Container App's system-assigned managed-identity principal ID.
- `keyVaultUri` — the Key Vault's `vaultUri` (e.g. `https://<name>.vault.azure.net/`).
- `serviceBusNamespaceEndpoint` — the Service Bus namespace's fully-qualified host name.
- `serviceBusQueueName` — the name of the provisioned queue.

## Verification

Confirm success at each stage by the observable outputs below:

1. **Pre-flight compile (Step 3):** `bicep build` completes with no error output and produces (or would produce, if `--outfile` were supplied) valid ARM JSON.
2. **Deployment result (Step 4):** `az deployment group create` returns `"provisioningState": "Succeeded"` in its JSON output. A `Failed` provisioning state means the deployment did not complete; read the returned error detail and re-run only after correcting the template or parameters — do not proceed to verification below on a `Failed` result.
3. **Container App reachability:** issue a basic HTTPS reachability check against the captured `containerAppFqdn`, targeting `OpenClaw.Core`'s health/status endpoint once the app is deployed there, for example:

   ```powershell
   Invoke-WebRequest -Uri "https://$containerAppFqdn/healthz" -UseBasicParsing
   ```

   A `200`-range HTTP status confirms the Container App is running and reachable. A connection failure or non-2xx status indicates the container did not start correctly or the health endpoint path differs; check the Container App's revision status and container logs before retrying.
4. **Key Vault RBAC authorization model:** confirm the vault was provisioned with the Azure RBAC permission model, not the legacy access-policy model:

   ```powershell
   az keyvault show --name '<key-vault-name>' --resource-group '<rg>' --query properties.enableRbacAuthorization
   ```

   This must return `true`.

   **Known follow-up, not a defect:** this feature's Bicep does not yet create a role assignment granting the Container App's managed identity (`containerAppPrincipalId`) access to the vault's data plane. Per the research artifact's Decision 2, the recommended least-privilege role for the future auth module's read-only secret/certificate access is `Key Vault Secrets User`. Until a future Bicep module wires this role assignment declaratively, create it manually as a follow-up step:

   ```powershell
   az role assignment create `
       --role "Key Vault Secrets User" `
       --assignee '<containerAppPrincipalId>' `
       --scope (az keyvault show --name '<key-vault-name>' --resource-group '<rg>' --query id -o tsv)
   ```

   Confirm the assignment exists:

   ```powershell
   az role assignment list --assignee '<containerAppPrincipalId>' --scope (az keyvault show --name '<key-vault-name>' --resource-group '<rg>' --query id -o tsv) --output table
   ```

   A row showing `Key Vault Secrets User` scoped to the vault confirms the assignment. Note that Azure RBAC role assignment propagation is not always instantaneous; allow a few minutes before re-checking if the assignment does not immediately appear in a subsequent list/read.
5. **Service Bus queue exists and is reachable:**

   ```powershell
   az servicebus queue show `
       --resource-group '<rg>' `
       --namespace-name '<service-bus-namespace-name>' `
       --name '<serviceBusQueueName>' `
       --output table
   ```

   A successful response describing the queue (including its `status: Active`) confirms the queue exists in the namespace. Cross-check the returned queue name against the `serviceBusQueueName` output captured in Step 5, and the namespace's host name against the captured `serviceBusNamespaceEndpoint`.
6. **Overall gate before enabling `OpenClaw:CloudSync:Enabled`:** all of items 2-5 above must be confirmed successful in the target environment before that flag is turned on in that environment, per the Cue above.

## Source and Citation

All step-by-step instructions above are Azure CLI / Bicep CLI steps; no third-party UI navigation is required, so the MCP-first/web-second UI sourcing order is not applicable to UI navigation. No callable MCP documentation-retrieval tool is wired into this repository at present (per the two-axis-model-selection spec, Out of Scope note); every citation below was therefore sourced via `WebFetch` against `learn.microsoft.com` (web-second, the sole available sourcing mechanism), captured 2026-07-07. The internal source for the deployment shape (resources, outputs, parameterization pattern) is `docs/features/active/2026-07-07-azure-bicep-iac-125/research/2026-07-07-bicep-iac-architecture.md`, captured 2026-07-07, and `docs/features/active/2026-07-07-azure-bicep-iac-125/spec.md` (Inputs/Outputs, API/CLI Surface sections), captured 2026-07-07.

| Step | Command / concept documented | Source URL | updated_at |
|---|---|---|---|
| Prerequisites (Bicep tooling, `.bicepparam` version requirements) | Create a parameters file for Bicep deployment | https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/parameter-files | 2026-06-17 |
| Step 3 | `bicep build` (Bicep CLI commands) | https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/bicep-cli#build | 2026-05-15 |
| Step 4 | `az deployment group create` (parameter precedence: file first, then `KEY=VALUE` override; bicepparam usage) | https://learn.microsoft.com/en-us/cli/azure/deployment/group?view=azure-cli-latest#az-deployment-group-create | 2026-04-07 |
| Step 4 (parameter override precedence and `.bicepparam` deployment syntax) | Create a parameters file for Bicep deployment — "Deploy Bicep file with parameters file" / Azure CLI section | https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/parameter-files | 2026-06-17 |
| Verification (Key Vault RBAC model, built-in `Key Vault Secrets User` role, `az role assignment create` pattern) | Grant permission to applications to access an Azure key vault using Azure RBAC | https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide | 2026-06-16 |
| Verification (Service Bus queue existence check) | `az servicebus queue show` | https://learn.microsoft.com/en-us/cli/azure/servicebus/queue?view=azure-cli-latest#az-servicebus-queue-show | 2026-04-07 |

Both Azure CLI reference pages above (`az deployment group create` and `az servicebus queue show`) share the same publisher `updated_at` value (2026-04-07) because they are generated from the same `azure-docs-cli` reference-documentation build; each command's content was independently confirmed present and current in the fetched page body.
