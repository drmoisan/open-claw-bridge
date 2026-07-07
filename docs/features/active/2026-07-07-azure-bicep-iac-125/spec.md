# 2026-07-07-azure-bicep-iac — Spec

- **Issue:** #125
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-07T01-02
- **Status:** Draft
- **Version:** 0.1

## Overview

The OpenClaw vision (`docs/open-claw-approach.master.md`, Stage 1 / Product Increment 1) targets an Azure-hosted service, but no Azure infrastructure-as-code exists in this repository. `deploy/` contains only Docker artifacts. F14 (`2026-07-03-graph-subscriptions-delta-117`, merged as PR #121) delivered the host-neutral `OpenClaw.Core.CloudSync` subsystem (Graph subscriptions, webhook notifications endpoint at `POST /graph/notifications`, an in-process `INotificationQueue` seam, and delta reconciliation) behind an opt-in `OpenClaw:CloudSync:Enabled` flag, explicitly deferring the Azure hosting target and a durable queue backend (Service Bus/Storage Queue) to this feature (F16 in the epic manifest; gap analysis item 16, Epic C). No concrete Bicep/ARM/Terraform IaC exists anywhere in the repository (verified by prior gap-analysis grep).


## Behavior

Author declarative Bicep IaC under a new `deploy/azure/` directory that provisions the Stage 1 cloud footprint for the existing, already-containerized `OpenClaw.Core` web host (`deploy/docker/openclaw-core.Dockerfile`):

- **Hosting**: an Azure Container App (or Container Apps environment) running the existing container image — the lowest-friction fit given `OpenClaw.Core` is a persistent ASP.NET Core web host already packaged as a container, rather than a Functions-triggered workload.
- **Key Vault**: a Key Vault instance for certificate/secret storage referenced by the app-only auth module (F12/F13), with no secrets committed to source control.
- **Queue**: provisioning for the durable queue backend (Azure Service Bus or Storage Queue) that will eventually back the `INotificationQueue` seam introduced by F14, without changing that seam's in-process default.

This is infrastructure authoring only — no runtime behavior change to `OpenClaw.Core` or `OpenClaw.Core.CloudSync`. Parameters must be environment-parameterized (e.g., dev/prod) and no live `az deployment` execution is expected to run in this environment (no Azure subscription/credentials are available in CI or in this workspace); declarative validation (`bicep build`/`bicep lint` or equivalent structural review) stands in for live deployment verification, and actual tenant deployment is deferred to a human-executed runbook, consistent with the F14/F15/F17 precedent for tenant-dependent steps.


## Inputs / Outputs

- **Inputs** are the Bicep parameter values consumed by `deploy/azure/main.bicep` at build/deploy time, not runtime CLI flags or environment variables of `OpenClaw.Core` itself:
  - `environmentName` (string) — logical environment label (e.g. `dev`, `prod`), used to derive resource names and tags. Default: `'dev'`.
  - `location` (string) — Azure region for all provisioned resources. Default: `resourceGroup().location` (inherits the resource group's region).
  - `resourceNamePrefix` (string) — naming prefix applied to all provisioned resource names (Container Apps environment, Key Vault, Service Bus namespace). Default: `'openclaw'`. This default is a reasonable convention chosen for this spec; it is not extracted from the research artifact or master vision doc, neither of which names a concrete prefix.
  - `containerImage` (string) — full reference (registry/repository:tag or `@sha256:` digest) to the `openclaw-core` container image built from `deploy/docker/openclaw-core.Dockerfile`. No default; this value changes per build and must be supplied explicitly.
- **Outputs** are the deployed-resource identifiers a future feature (e.g., wiring `OpenClaw.Core.CloudSync` to a durable queue, or the F12/F13 auth module to Key Vault) would consume. Per the Key Vault decision (RBAC, not access policies) and Service Bus decision in the research artifact, no output ever carries a literal secret or connection string:
  - `containerAppFqdn` (string) — the Container App's public fully-qualified domain name.
  - `containerAppPrincipalId` (string) — the Container App's system-assigned managed-identity principal ID, for use in a future Key Vault / Service Bus role assignment.
  - `keyVaultUri` (string) — the Key Vault's `vaultUri` (e.g. `https://<name>.vault.azure.net/`).
  - `serviceBusNamespaceEndpoint` (string) — the Service Bus namespace's fully-qualified host name (e.g. `<name>.servicebus.windows.net`), not a connection string.
  - `serviceBusQueueName` (string) — the name of the provisioned queue.
- **Config keys and defaults**: the above parameter names and defaults are bound concretely in `deploy/azure/parameters/main.dev.bicepparam` via `using 'main.bicep'` (per the research artifact's Decision 4 parameterization pattern), which sets `environmentName = 'dev'` and leaves `containerImage` to be supplied at deploy time (no dev-time default, since no image has been pushed to a registry from this workspace).
- **Versioning or backward-compatibility constraints**: none yet — this is the first version of the templates. The `main.<env>.bicepparam` naming convention accommodates adding `main.prod.bicepparam` later without restructuring `main.bicep` or its module contracts.

## API / CLI Surface

This feature adds no application API. Its "surface" is the Bicep template's parameter and output contract, plus the two CLI commands that invoke it.

- **Parameters** (declared on `main.bicep`, see Inputs / Outputs above): `environmentName` (string, default `'dev'`), `location` (string, default `resourceGroup().location`), `resourceNamePrefix` (string, default `'openclaw'`), `containerImage` (string, required).
- **Outputs** (declared on `main.bicep`): `containerAppFqdn` (string), `containerAppPrincipalId` (string), `keyVaultUri` (string), `serviceBusNamespaceEndpoint` (string), `serviceBusQueueName` (string).
- **CLI invocations**:
  - `bicep build deploy/azure/main.bicep` — compiles the template and all referenced modules to ARM JSON with zero errors; this is the in-scope, automatable command, run both locally (where the Bicep CLI is available) and in CI via `.github/workflows/_bicep-validate.yml`.
  - `az deployment group create --resource-group <rg> --template-file deploy/azure/main.bicep --parameters deploy/azure/parameters/main.dev.bicepparam` — the live-deployment command. This command is explicitly **out of scope for automated execution** in this feature: no Azure subscription or credentials exist in this workspace or in `ci.yml` (verified in the research artifact — no `azure/login` step or Azure secret reference anywhere in `.github/workflows/`). Execution is deferred to a human-executed runbook, tracked as a `human_interaction` exception (see Implementation Strategy).
- **Contracts and validation rules**: `bicep build` enforces Bicep's own type/parameter contract (required vs. defaulted parameters, type mismatches fail the build); the parameter-file secret-pattern scan script (see Implementation Strategy) additionally asserts no `.bicepparam`/JSON parameter file under `deploy/azure/parameters/` contains a literal value matching a secret-shaped pattern (connection string, key, token).

## Data & State

This feature introduces no application data or state changes to `OpenClaw.Core` or `OpenClaw.Core.CloudSync`. Explicitly, there are **no `INotificationQueue`/`ChannelNotificationQueue` C# code changes** — the in-process channel-based queue introduced by F14 remains the default; this feature only provisions the Azure Service Bus namespace/queue that a future feature would target.
- **Data transformations and invariants**: none at the application-data level. The relevant "state" is Azure resource state, provisioned declaratively. `bicep build` (and, later, an actual `az deployment`) is idempotent: re-running it against unchanged template/parameter input is a no-op (ARM's declarative deployment model reconciles the target state to match the template rather than re-creating resources).
- **Caching or persistence details**: not applicable; no cache or persistent store is introduced by this feature. The Key Vault and Service Bus resources are provisioned but not yet consumed by any code path.
- **Migration or backfill requirements**: none. No existing data is migrated; this is net-new infrastructure with no prior Azure footprint to reconcile against (confirmed in the research artifact: `deploy/azure/` does not currently exist).

## Constraints & Risks

- No Azure subscription/credentials exist in this environment or in CI (`ci.yml` runs on `windows-latest` GitHub-hosted runners with no Azure secrets configured); IaC authoring is fully automatable in-repo, but execution is not (per `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, Automation Feasibility table).
- Depends on the F14 hosting-target groundwork (`2026-07-03-graph-subscriptions-delta-117`, merged into `epic/openclaw-vision-integration` at commit `d67dea0`) which established that `OpenClaw.Core` (a persistent containerized ASP.NET Core web host) carries the webhook endpoint and queue seam this IaC will host.
- Risk: choosing the wrong hosting primitive (Functions vs. Container Apps) creates rework; Container Apps is favored because the container image already exists and the workload is a long-running host, not a function-triggered workload (master §8.3 lists Container Apps as a strong second choice specifically "when the worker or OpenClaw runtime is already containerized").


## Implementation Strategy

- **Implementation scope** (files added; per the research artifact's Decisions 1–5, treated as settled): this feature is additive-only in `deploy/` and `.github/workflows/`, plus one new script pair under `scripts/`/`tests/scripts/`. No existing production C#/PowerShell source file is modified other than the CI wiring edit below.
  - `deploy/azure/main.bicep` (new) — orchestrates the three modules below and declares the outputs listed in API / CLI Surface.
  - `deploy/azure/modules/containerApp.bicep` (new) — Azure Container Apps Environment + Container App running the existing `openclaw-core` image.
  - `deploy/azure/modules/keyVault.bicep` (new) — Key Vault with `enableRbacAuthorization: true`.
  - `deploy/azure/modules/queue.bicep` (new) — Azure Service Bus namespace + queue.
  - `deploy/azure/parameters/main.dev.bicepparam` (new) — dev-environment parameter binding via `using 'main.bicep'`.
  - `deploy/azure/README.md` (new) — short doc describing the module layout, the two CLI commands, and pointing to the deployment runbook (authored separately; this feature does not author runbook contents).
  - `.github/workflows/_bicep-validate.yml` (new) — reusable workflow declaring `on: { workflow_call:, workflow_dispatch: }`, running `bicep build`/`az bicep build` against `deploy/azure/main.bicep` and the parameter-file secret-pattern scan script below.
  - `scripts/Test-OpenClawBicepParameterSecrets.ps1` (new) — PowerShell script asserting no `.bicepparam`/JSON parameter file under `deploy/azure/parameters/` contains a literal secret-shaped value.
  - `tests/scripts/Test-OpenClawBicepParameterSecrets.Tests.ps1` (new) — Pester test for the script above, per this repo's test-location convention (`.claude/rules/general-unit-test.md`).
- **Existing files changed**: `.github/workflows/ci.yml` — one wiring edit, adding a job that references `uses: ./.github/workflows/_bicep-validate.yml`. No other existing production file is removed or changed.
- **New classes/functions/commands to add or update**: no C#/.NET classes are added or changed. The only new "commands" are the two CLI invocations documented in API / CLI Surface and the PowerShell script above.
- **Dependency changes**: none. Azure CLI and Bicep are preinstalled on the `windows-latest` GitHub-hosted runner (verified in the research artifact via the `actions/runner-images` documentation), so no new package or tool installation step is required in CI. No NuGet, npm, or PowerShell module dependency is added.
- **Logging/telemetry additions**: none. This feature has no runtime component to log from; `bicep build` and the secret-scan script surface their own pass/fail output via the CI job's console output.
- **Rollout plan**: no feature flag is needed — this is infrastructure provisioning, not a runtime behavior change, so there is nothing to gate behind `OpenClaw:CloudSync:Enabled` or any other flag. Live deployment (the `az deployment group create` command) is deferred to a human-executed runbook; this feature records that as a `human_interaction` requirement with `response: "exception"` and a `runbook_path`, consistent with the F11 precedent cited in the research artifact. The runbook's contents are out of scope for this spec and will be authored as a separate artifact by another agent.

## Definition of Done

- [ ] Acceptance criteria documented and mapped to tests or demos
- [ ] Behavior matches acceptance criteria in all documented environments
- [ ] Tests updated/added (unit/integration as applicable)
- [ ] Edge cases and error handling covered by tests
- [ ] Docs updated (README, docs/features/active/... links)
- [ ] Telemetry/logging added or updated (if applicable)
- [ ] Toolchain pass completed (format → lint → type-check → test)

## Seeded Test Conditions (from potential)
- [x] Bicep structural/declarative validation (`bicep build`, `bicep lint`) covering all new templates.
- [x] Parameter-file validation ensuring no secret values are inlined.
- [x] Documentation/runbook review for the live-deployment human-interaction exception.
