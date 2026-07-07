# `2026-07-07-azure-bicep-iac` — User Story

- Issue: #125
- Owner: drmoisan
- Status: Draft
- Last Updated: 2026-07-07T01-02

## Story Statement

- As a platform/DevOps engineer preparing the Stage 1 cloud rollout, I want parameterized Bicep templates that declaratively provision the Container Apps hosting target, an RBAC-scoped Key Vault, and a Service Bus queue, so that I can validate the infrastructure definition (`bicep build`) in CI before any live tenant deployment is attempted, without hand-authoring ARM JSON or re-deriving the hosting decision each time.
- As the on-call operator who will eventually enable `OpenClaw:CloudSync:Enabled` in production, I want the Key-Vault-backed, RBAC-scoped hosting target provisioned and validated ahead of time, so that when F14's CloudSync subsystem is ready to go live, the deployment target already exists as reviewed, version-controlled infrastructure rather than being improvised at cutover time.

## Problem / Why

The OpenClaw vision (`docs/open-claw-approach.master.md`, Stage 1 / Product Increment 1) targets an Azure-hosted service, but no Azure infrastructure-as-code exists in this repository. `deploy/` contains only Docker artifacts. F14 (`2026-07-03-graph-subscriptions-delta-117`, merged as PR #121) delivered the host-neutral `OpenClaw.Core.CloudSync` subsystem (Graph subscriptions, webhook notifications endpoint at `POST /graph/notifications`, an in-process `INotificationQueue` seam, and delta reconciliation) behind an opt-in `OpenClaw:CloudSync:Enabled` flag, explicitly deferring the Azure hosting target and a durable queue backend (Service Bus/Storage Queue) to this feature (F16 in the epic manifest; gap analysis item 16, Epic C). No concrete Bicep/ARM/Terraform IaC exists anywhere in the repository (verified by prior gap-analysis grep).


## Personas & Scenarios

- **Persona: DevOps engineer preparing the Product Increment 1 rollout.**
  - Who they are: an engineer on the OpenClaw team responsible for standing up the Stage 1 Azure footprint described in `docs/open-claw-approach.master.md`.
  - What they care about: that the hosting target matches the already-containerized `OpenClaw.Core` host (no rework from a mismatched hosting primitive), that no secret or connection string is ever committed to source control, and that the templates can be checked for correctness before any tenant credential is available.
  - Their constraints: no Azure subscription or credentials exist in this workspace or in `ci.yml`; any change touching `deploy/azure/**` must be verifiable through structural validation alone (`bicep build`), not through a live deployment.
  - Their goals and frustrations: they want a reviewable, version-controlled definition of the Container Apps + Key Vault + Service Bus footprint; their frustration, absent this feature, is that the only way to know a template is correct is to hand it to a tenant administrator and wait for a live deployment result.
  - Their context and motivations: F14 already built the CloudSync subsystem behind an opt-in flag and explicitly deferred the hosting target and durable queue backend to this feature; the engineer's task is to close that gap without touching F14's code.
- **Scenario: validating the templates before handoff to a tenant administrator.**
  - Who is acting: the DevOps engineer above.
  - What triggered the action: F16 is ready for review and the engineer needs to confirm the templates are structurally correct before opening a pull request.
  - What steps they take: they run `bicep build deploy/azure/main.bicep` locally (or, if the Bicep CLI is unavailable locally, trigger the `_bicep-validate.yml` workflow via `workflow_dispatch`); they confirm the build produces zero errors, that the compiled template defines a Container App, a Key Vault with `enableRbacAuthorization: true`, and a Service Bus namespace and queue, and that the parameter-file secret-pattern scan reports no matches against `deploy/azure/parameters/main.dev.bicepparam`.
  - What obstacles or decisions occur: the engineer confirms that no Azure credentials are available to actually run `az deployment group create` in this environment, so live deployment is out of scope here; that step is handed off separately, per the runbook, to a tenant administrator.
  - What outcome they expect: a merged, CI-validated set of Bicep templates that a tenant administrator can later deploy following the runbook, with no secret ever having been inlined or committed.


## Acceptance Criteria

- [x] `deploy/azure/` contains parameterized Bicep templates that declaratively provision: (a) an Azure Container Apps environment + Container App hosting the existing `openclaw-core` container image, (b) an Azure Key Vault instance, and (c) queue infrastructure (Service Bus namespace/queue or Storage Queue) suitable for a future durable `INotificationQueue` implementation.
- [x] No secrets, connection strings, or credentials are committed; secret-shaped parameters are marked `@secure()` and sourced from Key Vault references or deployment-time parameters only.
- [x] Templates are parameterized per environment (at minimum a `main.bicep` + a `parameters.<env>.json`/`.bicepparam` pair for at least one environment).
- [x] Bicep templates pass declarative structural validation (`bicep build` and, where available, `bicep lint`/`az bicep build --stdout`) with zero errors; if the `bicep` CLI is unavailable in this environment, structural review + documented rationale substitutes and is recorded as such.
- [x] Live tenant deployment (`az deployment group create`) is explicitly out of scope for automated execution and is documented as a `human_interaction` exception with a runbook, consistent with the F11/F14/F15/F17 precedent.
- [x] No changes to `OpenClaw.Core` or `OpenClaw.Core.CloudSync` runtime behavior; this is IaC-only.


## Non-Goals

This feature does **not**:

- Execute a live Azure deployment. `az deployment group create`/`az deployment sub create` execution against a real subscription is out of scope for automated execution in this environment and in CI; it is deferred to a human-executed runbook and tracked as a `human_interaction` exception.
- Wire `OpenClaw.Core`/`OpenClaw.Core.CloudSync` to consume the provisioned Service Bus queue. The `INotificationQueue` seam stays on its in-process `ChannelNotificationQueue` default introduced by F14; no Azure SDK dependency or code change is added to either project.
- Create the Entra app registration or its RBAC scope. That work was already completed and merged by F11/F12 (`exchange-rbac-scripts-111` and the related app-only auth module); this feature only provisions a Key Vault that a future consumer of those credentials could reference.
- Enable `OpenClaw:CloudSync:Enabled` in any environment. That flag's runtime behavior is unchanged by this feature; provisioning the hosting target does not turn CloudSync on anywhere.
