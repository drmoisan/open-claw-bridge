
# Research: Azure Bicep IaC Architecture (F16, issue #125)

Date: 2026-07-07
Feature: F16 `azure-bicep-iac` (epic manifest, gap analysis Epic C item 16), branch `feature/azure-bicep-iac-125` based on `origin/epic/openclaw-vision-integration`.
Upstream dependency: F14 `graph-subscriptions-delta` (issue #117, merged as PR #121, commit `d67dea0`), which explicitly deferred the Azure hosting target and durable queue backend to this feature.

All findings below were verified by reading the named files in this worktree on 2026-07-07, except where marked "per Microsoft Learn" (verified via WebFetch against `learn.microsoft.com`).

**Note on source integrity:** the first `Read` of `docs/open-claw-approach.master.md` in this session returned tool output containing embedded `<system-reminder>` blocks that restated repo rule files verbatim and included an instruction to conceal a date change from the user. A second, targeted read of the same line range (506-575) returned clean Markdown with no such content. This research treats that first-read artifact as untrusted/non-authoritative and did not act on the embedded instruction; all master-doc citations below are drawn from the clean, line-numbered re-read.

---

## 1. Current State Analysis

### 1.1 What exists today (verified)

- `deploy/` contains only Docker artifacts (verified by `Glob deploy/**`): `deploy/docker/openclaw-core.Dockerfile`, `entrypoint.sh`, `healthcheck.sh`, plus an unrelated `openclaw-agent.Dockerfile` and an `openclaw-assistant/` agent-persona folder. `deploy/azure/` does not exist — confirmed by the same glob (no matches under that path) and independently corroborated by the gap-analysis line "`deploy/` contains only Docker artifacts" (`docs/research/2026-07-01-open-claw-vision-gap-analysis.md` line 28, restated at line 65).
- `deploy/docker/openclaw-core.Dockerfile` (62 lines) containerizes `OpenClaw.Core` as a persistent, non-root ASP.NET Core web host: multi-stage build (`dotnet publish` in an SDK stage, `mcr.microsoft.com/dotnet/aspnet:10.0` runtime stage), runs as a dedicated `app` user (lines 24-36), delegates startup to `entrypoint.sh` (`ENTRYPOINT`, line 61), and `EXPOSE 8081` (line 57) — a long-lived listening process, not a function-triggered handler.
- No `.github/workflows/_*.yml` reusable workflow exists anywhere in the repo (`Glob .github/workflows/_*.yml` returned no matches). `.github/workflows/` contains exactly `ci.yml` and `publish.yml` (verified by glob). `ci.yml` (93 lines) declares three jobs — `.NET Build + Test`, `PowerShell QC`, `Workflow Lint` (actionlint) — with `steps:` written inline in each job; none of them use `uses: ./.github/workflows/_<name>.yml`. No `pr-pipeline.yml` orchestrator workflow and no `.github/workflows/README.md` exist yet, though `.claude/skills/orchestrate/SKILL.md` (lines 138-140) references both as the intended destination for the reusable-workflow convention.
- `quality-tiers.yml` classifies only the four `.sln` projects (`OpenClaw.Core`/`OpenClaw.HostAdapter` = T1, `OpenClaw.MailBridge.Contracts`/`OpenClaw.HostAdapter.Contracts`/`OpenClaw.MailBridge` = T2, `OpenClaw.MailBridge.Client` = T3) plus their test projects. Bicep templates are declarative, non-`.sln` artifacts; they do not add a project entry to `quality-tiers.yml` in the way a new C#/PowerShell project would.
- Neither `bicep` nor `az` CLI is installed in this local workspace (verified: no output from `which bicep` / `which az`), so no local `bicep build` validation could be run as part of this research.

### 1.2 F14's explicit deferral to F16 (verified, `docs/features/active/2026-07-03-graph-subscriptions-delta-117/spec.md`)

- Line 14: "Everything is unit-testable now; Azure hosting and queue backends arrive with F16 behind the same seams."
- Line 54 (Design Decision D-4): `ChannelNotificationQueue` is an in-process bounded `System.Threading.Channels` queue (`QueueCapacity` default 1000, `BoundedChannelFullMode.DropWrite` + Warning log). "Azure Service Bus/Storage Queue implementations are F16 deployment concerns behind this same interface; no Azure SDK dependency is added in this feature."
- Line 115: "Live subscription creation requires a tenant + public webhook URL: out of scope; covered by runbook/deployment work (F16) — no live calls in tests... F16 adds deployment." Also: "No new `human_interaction` requirement is added by this feature: every deliverable here is locally verifiable code."
- Line 116: "Azure queue implementations deferred behind `INotificationQueue` (in-process channel now) — explicit F16 note; do NOT add Azure SDK dependencies in this feature."
- Line 137: "Rollout: local default OFF... Enabling requires the Graph backend (D-7) and a reachable `NotificationUrl`, which arrives with F16 deployment."
- Line 18: the webhook endpoint (`POST /graph/notifications`) is "Mapped on the Core host only when `OpenClaw:CloudSync:Enabled=true`" — i.e., inside `OpenClaw.Core`'s own ASP.NET Core composition root (`Program.cs`, D-6, line 56), not as a separate Azure Functions app.

### 1.3 F16 spec/plan already drafted in this feature folder (verified)

`docs/features/active/2026-07-07-azure-bicep-iac-125/spec.md` (Draft, v0.1) already states the intended shape: Container App hosting the existing image, a Key Vault for cert/secret storage, and queue provisioning for the F14 `INotificationQueue` backend, "without changing that seam's in-process default" (spec.md line 21) — i.e., F16 provisions Azure resources but does not wire the app to consume them yet. This research confirms and grounds each of those choices with primary-source evidence and adds the parameterization and CI-validation detail the spec does not yet cover.

---

## 2. Decision 1 — Hosting Primitive: Azure Container Apps

**Recommendation:** an Azure Container Apps Environment + Container App resource running the existing `openclaw-core` image, port 8081.

**Evidence:**
- Master doc §8.3 (`docs/open-claw-approach.master.md` lines 560-567) is an "Implementation Options Comparison" table written for a *new-build* MVP, before `OpenClaw.Core` existed as a container. It rates "Azure Functions (HTTP webhook + queue-trigger worker)" as "**Best overall fit for the selected design**" (line 564) and "Azure Container Apps (webhook API + background worker)" as "Good second choice," explicitly noting Container Apps is a "Strong option when the worker or OpenClaw runtime is already containerized" (line 565).
- That precondition is now true: `OpenClaw.Core` is already containerized (`deploy/docker/openclaw-core.Dockerfile`) and already carries the webhook endpoint and CloudSync workers as an in-process ASP.NET Core host (F14 spec.md line 18, D-6). Splitting the webhook validation endpoint from a queue-triggered worker — the scenario Functions was optimized for — was already rejected by F14's own design: F14 built one endpoint inside one persistent host, not two separately deployed handlers.
- Repackaging `OpenClaw.Core` as an Azure Functions app would require restructuring the composition root (`Program.cs`) and the hosted-service model (`SubscriptionRenewalWorker`, `DeltaReconciliationWorker`, `NotificationDispatchWorker` are all long-running `BackgroundService` instances per F14 spec.md lines 32/43/45, not short-lived function invocations), which is out of scope for an IaC-only feature and would contradict F14's D-6 opt-in-inside-`Program.cs` design.
- Container Apps also natively supports scale-to-zero and HTTP-triggered scaling if cost reduction is later desired, without requiring the app to be re-architected as short-lived functions.
- **App Service** was not evaluated in depth: it is not named in either the master doc §8.3 comparison or the gap analysis, and offers no advantage over Container Apps for an already-containerized workload; it is noted here only to record that it was considered and set aside.

**Rejected alternative (brief):** Azure Functions — best fit for a greenfield split-webhook/queue-trigger design (master §8.3), but does not match the containerized, persistent-host shape F14 already built.

---

## 3. Decision 2 — Key Vault Authorization Model: Azure RBAC

**Recommendation:** `enableRbacAuthorization: true` on the Key Vault resource (the Azure RBAC permission model), not the legacy vault access-policy model.

**Evidence:**
- Master doc line 330: "Store the certificate or secret in Key Vault or the equivalent secret manager... Use MSAL's 'acquire silently' pattern... Prefer certificate auth to client secrets" — establishes that a future app-only auth module (F12/F13, already merged) is the intended consumer of vault-stored credentials, though no code change to that module is part of this feature (per F16 spec.md line 21's "provisioning ... without changing" pattern).
- Master doc §8.1 reference architecture (line 523): `Vault[Key Vault\n(cert/secret)]` feeding the worker — confirms Key Vault's role in the reference architecture this feature realizes as IaC.
- Per Microsoft Learn (`learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide`, verified via WebFetch): "Starting with API version 2026-02-01, Azure RBAC is the default access control model for newly created key vaults." The same page states the access-policy model is now labeled "(legacy)" in the data-plane access-control comparison table. Azure RBAC additionally provides built-in least-privilege roles (`Key Vault Secrets User`, `Key Vault Certificate User`, etc.) scoped at the vault, resource-group, or subscription level, which composes cleanly with the master doc's least-privilege posture (§7.1).
- No live secret values are placed in the Bicep templates or parameter files: Bicep's `getSecret()` function (confirmed in the parameter-files documentation, `learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/parameter-files`) is the documented mechanism for referencing a Key Vault secret at deployment time without inlining the value, and is the pattern to use if a parameter file ever needs to pass a Key-Vault-backed value to a downstream resource.

**Rejected alternative (brief):** vault access-policy model — legacy per current Microsoft guidance, coarser-grained (policies are per-vault-only, no resource-group/subscription scoping), and superseded as the default for new vaults.

---

## 4. Decision 3 — Queue Backend: Azure Service Bus (namespace + queue)

**Recommendation:** an Azure Service Bus namespace with a single queue, provisioned as the durable backend the `INotificationQueue` seam will eventually target — with Azure Storage Queue documented as an alternative, not pursued further in this feature.

**Evidence:**
- F14 spec.md line 54 (D-4) frames the in-process `ChannelNotificationQueue` as explicitly a stopgap: bounded capacity, `DropWrite`-on-full semantics, with correctness recovered only via the periodic delta reconciliation (spec.md lines 41-43), not via queue durability. The webhook is a "wake signal," not the source of truth (master doc's design principle, restated in F14 spec.md D-2, line 52).
- Master doc §8.1 (line 520) names the queue tier generically as `Queue\n(Service Bus / Storage Queue)`, leaving the concrete choice open — this is the choice F16 is asked to close (gap-analysis line 106: "Depends on: item 14 (needs a concrete hosting target decision)").
- Service Bus offers dead-lettering, at-least-once delivery with peek-lock, and (if ever needed) sessions — properties that match a "durable, at-least-once" tier sitting behind a webhook whose current in-process implementation already accepts silent drops under load (F14 D-4) and relies on a separate reconciliation mechanism to recover from any loss. Provisioning the more durable option now means a future migration from the in-process channel to a real queue does not also require a second migration from Storage Queue to Service Bus if delivery-guarantee gaps surface later.
- Storage Queue remains simpler and cheaper (no dedicated namespace, coarser at-least-once semantics, no dead-lettering, 7-day maximum TTL) and is retained as the documented fallback if Service Bus's namespace-level cost is judged disproportionate for the traffic volume (single-mailbox Inbox subscriptions per F14 spec.md line 31 — "One subscription per principal Inbox").
- Per the F16 spec.md (line 21) and the gap analysis (line 106), this feature provisions the Bicep resource only; it does not modify `INotificationQueue` or add an Azure SDK dependency to `OpenClaw.Core` (F14 spec.md line 116's constraint is a same-branch/near-term constraint this feature must not violate by introducing an SDK reference in application code — only the ARM/Bicep resource definition is added).

**Rejected alternative (brief):** Azure Storage Queue — cheaper and simpler, retained as a documented fallback; rejected as the primary choice because it lacks dead-lettering and richer delivery semantics that better match the durable-queue role described for this seam.

---

## 5. Decision 4 — Parameterization Pattern

**Recommendation:** a module-per-resource `main.bicep` composition with native `.bicepparam` files, one per environment.

```
deploy/azure/
  main.bicep                    # orchestrates the modules below, declares outputs
  modules/
    containerApp.bicep          # Container Apps Environment + Container App
    keyVault.bicep               # Key Vault, enableRbacAuthorization: true
    queue.bicep                  # Service Bus namespace + queue
  parameters/
    main.dev.bicepparam          # using 'main.bicep' — dev environment values
```

**Evidence:**
- Per Microsoft Learn (`learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/parameter-files`, verified via WebFetch): Bicep supports both `.bicepparam` (native) and JSON parameter files; `.bicepparam` files use a `using '<path>/main.bicep'` statement to bind to the template, support typed parameters, expressions, and variables, and Microsoft's documented naming convention for multi-environment files is `main.dev.bicepparam` / `main.prod.bicepparam`. This is the format recommended in current documentation over hand-written JSON parameter files for new work, and both `az deployment group create --parameters storage.bicepparam` and the Bicep CLI (`bicep build-params`) support it directly.
- A modules-per-resource split (`containerApp.bicep`, `keyVault.bicep`, `queue.bicep`) mirrors the general-code-change policy's "reusability" and "separation of concerns" priorities (`.claude/rules/general-code-change.md`): each module is independently reviewable and independently referenced from `main.bicep`, and a future feature that needs only the Key Vault module (e.g., a standalone secret-rotation script) can reference it without pulling in the Container App/queue modules.
- Only one environment (`dev`) is required by the F16 spec.md scope ("at least one environment"); the file-naming convention accommodates adding `main.prod.bicepparam` later without restructuring `main.bicep`.

---

## 6. Decision 5 — CI Validation Feasibility

**Finding: feasible.** Per the public `actions/runner-images` documentation for the Windows Server 2022 image (`windows-latest`), verified via WebFetch: Azure CLI 2.87.0 is preinstalled under "CLI Tools," and Bicep 0.44.1 is listed separately as an installed tool. Both `az bicep build` and the standalone `bicep build` command are therefore available on the hosted runner with no additional install step, making a genuine automated `bicep build` (and optionally `bicep lint`) validation step feasible in CI — not merely a local structural review.

**Wiring pattern to follow (per `.claude/skills/orchestrate/SKILL.md` lines 138-140):** "Every new CI gate in this repository ships as a callable reusable workflow named `_<name>.yml` that declares both `on: workflow_call:` and `on: workflow_dispatch:`. Orchestrator workflows... reference these callees via `uses: ./.github/workflows/_<name>.yml`."

**Important caveat verified in this repo:** no precedent for this pattern exists yet. `Glob .github/workflows/_*.yml` returned no matches, and no `pr-pipeline.yml` orchestrator workflow exists (only `ci.yml` and `publish.yml`, both with only inline `steps:`, verified by reading `ci.yml` in full). F16 would be the **first** feature to introduce a `_<name>.yml` reusable workflow in this repository. The concrete implication:

1. Create `.github/workflows/_bicep-validate.yml` declaring `on: { workflow_call:, workflow_dispatch: }`, with a job that checks out the repo and runs `bicep build deploy/azure/main.bicep` (or `az bicep build --file ...`) plus, optionally, `bicep lint`.
2. Because no separate `pr-pipeline.yml` orchestrator exists, wire the new callee directly into `ci.yml` as an additional job using `uses: ./.github/workflows/_bicep-validate.yml` — the closest available application of the documented convention given the repo's current (pre-orchestrator-split) workflow layout. This keeps `_bicep-validate.yml` independently dispatchable (satisfying the `workflow_dispatch` requirement) while still gating pull requests through `ci.yml`.
3. The existing `Workflow Lint` job in `ci.yml` (lines 79-93, `actionlint`) will lint the new workflow's YAML syntax automatically once it is added — no separate lint step is needed for the workflow file itself.

**Rule applicability check (both already-loaded policies considered):**
- `.claude/rules/benchmark-baselines.md` — **not applicable.** It governs performance-baseline provenance for benchmark regression gates; a Bicep-validation step produces no `HostEnvironmentInfo`/baseline artifact and consumes no committed baseline, so this rule imposes no obligation on F16.
- `.claude/rules/ci-workflows.md` — **conditionally applicable.** It applies only if the new step's `run:` block intentionally invokes a command expected to fail (e.g., a negative-path self-test asserting `bicep build` rejects an intentionally malformed template to prove the gate works). The simplest design avoids this entirely: run `bicep build` only against the real, valid templates and let a non-zero exit code fail the step naturally (no reset needed because failure is not expected on the happy path). If the atomic plan later adds a defense-in-depth negative self-test, that step's `pwsh` block must explicitly reset `$LASTEXITCODE = 0` after the expected failure or terminate with `exit 0`/`exit 1` per the rule's two permitted patterns — do not let a residual non-zero exit from the intentionally-failing nested command propagate.

---

## Rejected Alternatives (summary)

- **Azure Functions hosting** — best fit for a greenfield split-webhook/queue-trigger MVP (master §8.3), but `OpenClaw.Core` is already a persistent containerized host with the webhook and workers mapped in-process (F14); repackaging would contradict F14's design and is out of scope for an IaC-only feature.
- **Key Vault access-policy model** — legacy per current Microsoft guidance; coarser-grained than Azure RBAC and no longer the default for new vaults.
- **Azure Storage Queue as primary** — simpler/cheaper but lacks dead-lettering and richer delivery guarantees; retained as a documented fallback, not the primary recommendation.
- **JSON parameter files as primary** — still valid and supported, but `.bicepparam` is Microsoft's current native format with typed parameters and a documented multi-environment naming convention; JSON parameter files remain a valid fallback if tooling constraints require them.

---

## 7. Behavior Semantics (for spec.md)

- **Success:** `bicep build` (locally or in CI) compiles `main.bicep` and all referenced modules with zero errors; the compiled ARM JSON contains a Container App, a Key Vault with `properties.enableRbacAuthorization == true`, and a Service Bus namespace + queue; no parameter file or template contains a literal secret/connection-string/key value.
- **Failure:** a malformed template (syntax error, missing required parameter, type mismatch) fails `bicep build` with a non-zero exit code, which fails the CI job — this is the ordinary (non-deliberate) failure path and needs no exit-code handling per §6 above.
- **Out of scope / explicitly not attempted:** actual `az deployment group create`/`az deployment sub create` execution against a live Azure subscription. No such execution occurs in this environment or in CI (no Azure credentials configured in `ci.yml`, verified by reading the full workflow — no `azure/login` step or `AZURE_CREDENTIALS` secret reference exists anywhere in `.github/workflows/`).
- **Edge case:** unlike F14 (spec.md line 115, "No new `human_interaction` requirement is added by this feature"), F16 **does** require a `human_interaction` requirement, because the live deployment step is not locally verifiable code — it is Automatable-with-credentials per §8 below, following the F11 (`exchange-rbac-scripts-111`) precedent of a scripts-plus-runbook deliverable with an `exception` response and a `runbook_path` into the feature folder (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md` is the structural precedent to follow for a new `deploy/azure/runbooks/` or feature-folder `runbooks/` deployment runbook).

## 8. Requirements Mapping (for spec.md / plan)

| Spec.md theme | Concrete Bicep artifact | State/transition |
|---|---|---|
| "IaC provisions the hosting target ... with no secrets committed" (gap analysis line 106) | `modules/containerApp.bicep` | Declares Container Apps Environment + Container App referencing the existing image by tag/digest parameter, not a hardcoded secret. |
| "Key Vault ... referenced by the app-only auth module" (spec.md line 20) | `modules/keyVault.bicep` | `enableRbacAuthorization: true`; role assignments scoped to the future app's managed identity (output from `containerApp.bicep`), not user accounts. |
| "provisioning for the durable queue backend ... without changing that seam's in-process default" (spec.md line 21) | `modules/queue.bicep` | Service Bus namespace + queue provisioned; no change to `INotificationQueue`/`ChannelNotificationQueue` or any C# project. |
| "parameterized per-environment" (gap analysis line 106) | `parameters/main.dev.bicepparam` | One file per environment; `using 'main.bicep'` binding; no secret values inlined. |

## 9. Testing Implications

- **Structural/declarative validation** (per F16 spec.md's Seeded Test Conditions, lines 72-74): `bicep build` and `bicep lint` against every template and module, run in CI via `_bicep-validate.yml` (feasible per §6). This is the closest analog to a "unit test" for declarative IaC and should run on every PR touching `deploy/azure/**`.
- **Parameter-file secret scan:** a lightweight structural check (grep-based or a small PowerShell script under `scripts/`) asserting no `.bicepparam`/JSON parameter file contains a literal value matching common secret-shaped patterns (connection strings, keys) — this can be authored and unit-tested entirely in-repo (no live subscription needed) and should sit alongside the `bicep build` step in `_bicep-validate.yml`.
- **No live deployment test is possible or expected** in this environment or in CI; live deployment verification is deferred to the human-executed runbook (§7, §10).
- Coverage-percentage gates (`.claude/rules/general-unit-test.md`) do not apply to declarative Bicep files themselves (no executable/test-file split exists for `.bicep`), but do apply normally to any new PowerShell validation script added under `scripts/` and its mirrored `tests/scripts/` test, per the existing repo convention (`.claude/rules/general-unit-test.md` Test File Location section).

---

## Automation Feasibility

| Requirement | Classification | Rationale | Recommended response |
|---|---|---|---|
| Author `main.bicep` + `modules/containerApp.bicep`, `keyVault.bicep`, `queue.bicep` | Fully automatable in-repo | Static declarative text; no live Azure state needed to write correct resource definitions. | N/A — proceed as a normal feature. |
| Author `.bicepparam` per-environment parameter files | Fully automatable in-repo | Same as above; typed parameter bindings validated by `bicep build`, not by a live subscription. | N/A — proceed as a normal feature. |
| `bicep build` / `bicep lint` structural validation | Fully automatable in CI; not available in this local sandbox (no `bicep`/`az` CLI installed here, verified) | Windows-latest hosted runner ships Azure CLI 2.87.0 and Bicep 0.44.1 preinstalled (verified via WebFetch of the runner-images documentation), so the check runs without new tooling installation in CI even though it cannot run locally in this workspace. | N/A for CI authoring — proceed; do not attempt to install/validate locally in this workspace. |
| Author `.github/workflows/_bicep-validate.yml` (workflow_call + workflow_dispatch) and wire into `ci.yml` | Fully automatable in-repo | Workflow YAML is static text; `actionlint` (already in `ci.yml`) lints it once added. | N/A — proceed as a normal feature. |
| Author a parameter-file secret-pattern scan script | Fully automatable in-repo | Pure text-pattern check; unit-testable with fixture strings, no live Azure calls. | N/A — proceed as a normal feature. |
| Executing `az deployment group create` / `az deployment sub create` against a real Azure subscription | Automatable-with-credentials | Requires a live Azure subscription and credentials that exist in neither this workspace nor `ci.yml` (no `azure/login` step or Azure secret configured anywhere in `.github/workflows/`, verified). | `exception` with a runbook: provide the deployment command(s) and a runbook for a human (or a separately-credentialed deployment pipeline) to execute; do not attempt from this environment or from unmodified CI. |
| Post-deployment verification (webhook reachability, Key Vault RBAC role assignment applied, Service Bus queue reachable from the future app identity) | Automatable-with-credentials | Requires the resources from the prior row to already exist in a live tenant. | `exception` with the same runbook; record as a `human_interaction` requirement (unlike F14, which needed none) with `response: "exception"` and a `runbook_path` into this feature's `runbooks/` folder, following the F11 precedent (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`). |

