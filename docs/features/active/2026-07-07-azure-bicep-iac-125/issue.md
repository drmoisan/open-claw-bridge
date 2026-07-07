# azure-bicep-iac (Issue #125)

- Date captured: 2026-07-07
- Author: drmoisan
- Status: Promoted -> docs/features/active/azure-bicep-iac/ (Issue #125)

- Issue: #125
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/125
- Last Updated: 2026-07-07
- Work Mode: full-feature

## Problem / Why

The OpenClaw vision (`docs/open-claw-approach.master.md`, Stage 1 / Product Increment 1) targets an Azure-hosted service, but no Azure infrastructure-as-code exists in this repository. `deploy/` contains only Docker artifacts. F14 (`2026-07-03-graph-subscriptions-delta-117`, merged as PR #121) delivered the host-neutral `OpenClaw.Core.CloudSync` subsystem (Graph subscriptions, webhook notifications endpoint at `POST /graph/notifications`, an in-process `INotificationQueue` seam, and delta reconciliation) behind an opt-in `OpenClaw:CloudSync:Enabled` flag, explicitly deferring the Azure hosting target and a durable queue backend (Service Bus/Storage Queue) to this feature (F16 in the epic manifest; gap analysis item 16, Epic C). No concrete Bicep/ARM/Terraform IaC exists anywhere in the repository (verified by prior gap-analysis grep).

## Proposed Behavior

Author declarative Bicep IaC under a new `deploy/azure/` directory that provisions the Stage 1 cloud footprint for the existing, already-containerized `OpenClaw.Core` web host (`deploy/docker/openclaw-core.Dockerfile`):

- **Hosting**: an Azure Container App (or Container Apps environment) running the existing container image — the lowest-friction fit given `OpenClaw.Core` is a persistent ASP.NET Core web host already packaged as a container, rather than a Functions-triggered workload.
- **Key Vault**: a Key Vault instance for certificate/secret storage referenced by the app-only auth module (F12/F13), with no secrets committed to source control.
- **Queue**: provisioning for the durable queue backend (Azure Service Bus or Storage Queue) that will eventually back the `INotificationQueue` seam introduced by F14, without changing that seam's in-process default.

This is infrastructure authoring only — no runtime behavior change to `OpenClaw.Core` or `OpenClaw.Core.CloudSync`. Parameters must be environment-parameterized (e.g., dev/prod) and no live `az deployment` execution is expected to run in this environment (no Azure subscription/credentials are available in CI or in this workspace); declarative validation (`bicep build`/`bicep lint` or equivalent structural review) stands in for live deployment verification, and actual tenant deployment is deferred to a human-executed runbook, consistent with the F14/F15/F17 precedent for tenant-dependent steps.

## Acceptance Criteria (early draft)

- [x] `deploy/azure/` contains parameterized Bicep templates that declaratively provision: (a) an Azure Container Apps environment + Container App hosting the existing `openclaw-core` container image, (b) an Azure Key Vault instance, and (c) queue infrastructure (Service Bus namespace/queue or Storage Queue) suitable for a future durable `INotificationQueue` implementation.
- [x] No secrets, connection strings, or credentials are committed; secret-shaped parameters are marked `@secure()` and sourced from Key Vault references or deployment-time parameters only.
- [x] Templates are parameterized per environment (at minimum a `main.bicep` + a `parameters.<env>.json`/`.bicepparam` pair for at least one environment).
- [x] Bicep templates pass declarative structural validation (`bicep build` and, where available, `bicep lint`/`az bicep build --stdout`) with zero errors; if the `bicep` CLI is unavailable in this environment, structural review + documented rationale substitutes and is recorded as such.
- [x] Live tenant deployment (`az deployment group create`) is explicitly out of scope for automated execution and is documented as a `human_interaction` exception with a runbook, consistent with the F11/F14/F15/F17 precedent.
- [x] No changes to `OpenClaw.Core` or `OpenClaw.Core.CloudSync` runtime behavior; this is IaC-only.

## Constraints & Risks

- No Azure subscription/credentials exist in this environment or in CI (`ci.yml` runs on `windows-latest` GitHub-hosted runners with no Azure secrets configured); IaC authoring is fully automatable in-repo, but execution is not (per `docs/research/2026-07-01-open-claw-vision-gap-analysis.md`, Automation Feasibility table).
- Depends on the F14 hosting-target groundwork (`2026-07-03-graph-subscriptions-delta-117`, merged into `epic/openclaw-vision-integration` at commit `d67dea0`) which established that `OpenClaw.Core` (a persistent containerized ASP.NET Core web host) carries the webhook endpoint and queue seam this IaC will host.
- Risk: choosing the wrong hosting primitive (Functions vs. Container Apps) creates rework; Container Apps is favored because the container image already exists and the workload is a long-running host, not a function-triggered workload (master §8.3 lists Container Apps as a strong second choice specifically "when the worker or OpenClaw runtime is already containerized").

## Test Conditions to Consider

- [x] Bicep structural/declarative validation (`bicep build`, `bicep lint`) covering all new templates.
- [x] Parameter-file validation ensuring no secret values are inlined.
- [x] Documentation/runbook review for the live-deployment human-interaction exception.

## Next Step

- [x] Promote to GitHub issue (feature request template)
- [x] Create `docs/features/active/azure-bicep-iac-<issue>/` folder from the template
