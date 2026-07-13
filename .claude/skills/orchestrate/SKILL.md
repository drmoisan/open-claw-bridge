---
name: orchestrate
description: Route a repository request through the deterministic orchestration workflow for feature, bug, research, planning, execution, and review handoffs.
argument-hint: "[objective]"
---

# Orchestrate Skill

This skill frames work for the already-active main session, which serves as the orchestrator runtime for end-to-end feature or bug delivery. Orchestration deliberately stays in the main session on user invocation (`/orchestrate <objective>`); the same procedure governs a delegated `Agent(orchestrator)` run from `epic-planner` or `epic-orchestrator`.

## Invocation Argument

When invoked as `/orchestrate`, the orchestration objective is:

$ARGUMENTS

A delegated `Agent(orchestrator)` run receives its objective in the delegation prompt instead.

Perform the scale assessment first: if the objective is epic-scale (names an epic manifest or requires multiple independently mergeable features), stop before any delegation and direct the user to `/epic-plan` (planning) or `/epic-run` (executing a prepared epic), per `## Change Budget Routing` in `.claude/agents/orchestrator.md`.

## Prerequisites

Before proceeding, the orchestrator must:

1. Read `CLAUDE.md` for repository tone policy and architectural context.
2. Read applicable `.claude/rules/` files for the languages in scope.
3. Read the policy files listed in the compliance reading order section of `CLAUDE.md`.

## Checkpoint Handling

On every invocation, the main session must:

1. Read `artifacts/orchestration/orchestrator-state.json` to check for existing state.
2. If a valid checkpoint exists with a matching objective, resume from the recorded `next_step`.
3. If no checkpoint exists or the objective is new, begin the orchestration lifecycle from the start.

### Model-choice reconciliation on resume

Because model selection is required once delegation occurs (see `## Model Selection`), a resuming orchestrator must repair a missing model choice deterministically before delegating at a delegating `next_step`. When the resumed `next_step` is a delegating step:

a. **Preflight the checkpoint.** Run the orchestrator-state validator with `--require-model-routing` (via `mcp__drm-copilot__validate_orchestration_artifacts` or the local CLI) against `artifacts/orchestration/orchestrator-state.json` before the first delegation. Record the result in a `model_routing_preflight` block `{ status ("pass"|"fail"), checked_at (ISO-8601 UTC), validator_command, output_summary }`.
b. **Recompute the floor.** For the upcoming phase, recompute the complexity floor with `Get-ComplexityFloor -SignalsPresent <names>` (`.claude/lib/model-routing/ModelRouting.psm1`); do not reimplement the formula.
c. **Record the assessment.** Write a `complexity_assessments[]` entry `{ phase, band, floor, signals_present[], rationale, assessed_at }` with `floor` equal to the recomputed value and `band >= floor`.
d. **Resolve and record the receipt.** Resolve the model with `Resolve-DelegationModel -Agent <agent> -Band <complexity_band> -FablePolicy <fable_policy>` (`.claude/lib/model-routing/ModelRouting.psm1`) and write a `model_routing_receipts[]` entry `{ agent, phase, complexity_band, fable_policy, table_model, clamped_from | null, model }`.
e. **Persist and delegate.** Persist the checkpoint, then delegate with `model` equal to the receipt's `model`.

The orchestrator MUST NOT delegate at a delegating `next_step` while `model_routing_preflight` status is `fail`; it repairs the missing choice (steps b-e) and re-preflights until the status is `pass`.

## Autonomous-Execution Mandate

The orchestrator must achieve all actions agentically with no human interaction; full autonomy is a hard requirement. A silent manual blocker discovered at the end of a workflow is a defect, not an acceptable outcome. Every unautomatable (human-interaction) requirement must be detected early, resolved by exactly one of three permitted responses, and recorded in orchestrator state.

### Detection points

- Unautomatable requirements are enumerated as mandatory-unachievable requirements **before kickoff** wherever they are knowable up front.
- Where research is needed to discover them, they MUST be surfaced **no later than the research stage**.
- Research that touches third-party UIs (for example the Azure portal / Entra admin center, Outlook desktop or mobile, the Microsoft 365 admin center) MUST include an explicit automation-feasibility / human-interaction assessment recorded under an `## Automation Feasibility` section in the research artifact.

### Three permitted responses

When a step cannot be performed without a human, the orchestrator chooses exactly one response per requirement and records it in orchestrator state under `human_interaction.requirements[]`:

1. **`scope_change`** — change the scope to remove the manual dependency (for example, replace a portal click with an `az` CLI step that runs unattended).
2. **`exception`** — permit an exception. This requires emitting a human-exception runbook (see below). The exception is unresolved until its runbook file exists on disk.
3. **`halt`** — halt until further instruction. A `halt` blocks DONE while present. A `halt` is recoverable: a later checkpoint update that resolves the requirement (to `scope_change` or a runbook-backed `exception`, or clears the halt) lifts the block.

### Exception-runbook requirement

On a permitted `exception`, the orchestrator delegates runbook authoring to `Agent(human-exception-runbook)`, which emits a human-readable runbook at `<FEATURE>/runbooks/<name>.runbook.md` and returns the `runbook_path`. The orchestrator records the returned repo-root-relative path in `human_interaction.requirements[].runbook_path`. The runbook contract — canonical path, the five required sections (Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation), and the MCP-first / web-second sourcing rule — is defined authoritatively in `.claude/skills/human-exception-runbook/SKILL.md`.

### Enforcement points

The mandate is enforced mechanically, so DONE cannot be written while a human-interaction requirement is unresolved:

- **Validator invariants.** The top-level `human_interaction.requirements[]` invariants — the `response` enum `scope_change` | `exception` | `halt`, and the exception-requires-runbook invariant (`response == "exception"` requires a non-empty `runbook_path`) — are enforced by `scripts/dev_tools/validate_orchestrator_state.py` and by `Test-HumanInteractionShape` in `.claude/hooks/validate-orchestrator-output.ps1`, per the documented contract in `.claude/rules/orchestrator-state.md`. Checkpoints without a `human_interaction` key stay valid, so existing checkpoints are unaffected.
- **Completion gate.** `Test-HumanInteractionShape` in `.claude/hooks/validate-orchestrator-output.ps1` blocks DONE when a requirement has no resolved `response`, a `response` outside the enum, any `response == "halt"`, or an `exception` whose `runbook_path` is missing/empty or whose file does not exist. An absent `human_interaction` key passes.
- **Research gate.** `Test-AutomationFeasibilitySection` in `.claude/hooks/validate-task-researcher-output.ps1` requires the `## Automation Feasibility` section for applicable autonomous-execution research artifacts and blocks otherwise; non-applicable research is unaffected.

## Delegation Model

After reading `artifacts/orchestration/orchestrator-state.json`, the main session delegates work exclusively through configured workers:

- `atomic-planner` — generates phased implementation plans
- `atomic-executor` — executes approved plans task-by-task
- `feature-review` — produces policy, code, and feature audit artifacts
- `task-researcher` — performs deep research and writes findings to the research path the orchestrator resolves before delegating: `docs/features/<feature>/research/` when an active `feature-folder` is in scope in `orchestrator-state.json`, otherwise `docs/research/` for one-off research. The orchestrator passes the resolved path in the delegation prompt.

The orchestrator does not perform deep implementation itself. It coordinates, tracks state, and enforces completion.

## Preparation Mode

A delegation prompt carrying the literal marker `Preparation mode: true` (issued by `epic-planner` per the `.claude/skills/epic-plan/SKILL.md` kickoff line) scopes the run to planning only:

- **Route.** Select `route_id: preparation`. The route's routing-matrix entry requires receipts for `task-researcher`, `prd-feature`, `atomic-planner`, and `atomic-executor`; the skills `orchestrate`, `feature-promotion-lifecycle`, and `atomic-plan-contract`; and the promotion plus validator MCP tools.
- **Scope.** Run promotion (MCP surface), research, feature documents (`spec.md`, `user-story.md`), atomic planning, and the atomic-executor preflight (precondition validation only, R2 semantics: iterate plan revisions until `PREFLIGHT: ALL CLEAR`). Atomic execution, PR authoring, CI monitoring, and feature review are out of scope and are executed later by `epic-orchestrator`.
- **Terminal checkpoint.** Stop with `completed_steps` containing `S3_promotion` and `S4_atomic_planning`, `next_step: "S5_atomic_execution"`, out-of-scope step statuses set to `not-applicable`, and `blocked_reason: "none"`. Do NOT assert completion (`next_step: "complete"`, `S12_complete`, or a `completed` step8/9/10 status): the run has no PR or CI evidence, and the route's `requires_ci_gate: false` exempts it from `ci_gate` at the completion validator instead.
- **Commit.** Commit the prepared feature folder and plan to the current branch (the worktree branch created off the epic integration branch) before stopping, and report the `plan-path` and preflight status in the final output.

## Model Selection

Model selection is a second axis, strictly separate from `route`. `route` (`small | large | remediation | preparation | epic`) is deterministic — file-count driven for `small`/`large`, marker-driven for `preparation` (the `Preparation mode: true` kickoff line) and `epic`; it governs `required_agents`, `required_skills`, and `required_mcp_tools` only. `route` is NOT an input to model selection anywhere. The sole feature-level input to the delegation model tier is a judgment-based `complexity_band` (`C1 | C2 | C3 | C4`). The authoritative values live in the `model_policy` block of `config/orchestration-routing.json`.

The two canonical, tested reference implementations express the formulas the orchestrator applies by judgment:

- `.claude/lib/model-routing/ModelRouting.psm1` (`Get-ComplexityFloor`) — the deterministic complexity-floor formula. Each present `[floor]` signal contributes a candidate band of `C3`; the floor is the maximum triggered candidate band; the floor never exceeds `C3`. C4 is never floor-forced; it is reached only by judgment.
- `.claude/lib/model-routing/ModelRouting.psm1` (`Resolve-DelegationModel`) — the delegation-model selection formula (base `complexity_to_model` table, the `preferred` overlay, and the `disabled` clamp).

The runnable reference the destination runtime applies is the `.claude`-resident PowerShell module above; the repository validator remains the Python authority (`scripts/dev_tools/compute_complexity_floor.py` and `scripts/dev_tools/resolve_delegation_model.py`), pinned to the same `config/orchestration-routing.json` truth table by a static config-parity test.

End-to-end procedure:

1. **Parse the kickoff marker.** Read the session `model_budget.fable_policy` value (`disabled | available | preferred`, default `disabled`) from the kickoff marker line. This is the only session-level model-budget switch.
2. **Assess `complexity_band` and record it.** For each assessed phase, judge the `complexity_band` against the `model_policy.complexity` signal catalog and anchors, then record a `complexity_assessments[]` entry `{ phase, band, floor, signals_present[], rationale, assessed_at }`. The recorded `floor` must equal `compute_complexity_floor(signals_present)`, and the assessed `band` must satisfy `band >= floor`. The floor is a lower bound only; it never raises a judgment or evaluates its merit.
3. **Run the per-delegation selection order.** For each delegation, resolve the model as `resolve_delegation_model(agent, complexity_band, fable_policy)`: the `table_model` is the `preferred` overlay value when (`fable_policy == "preferred"` and the agent is in the overlay set `{atomic-planner, prd-feature, feature-review, task-researcher}` and `band == "C3"`), otherwise the base `complexity_to_model[band]`. Under `fable_policy == "disabled"`, a `fable` `table_model` clamps to `model = "opus"` with `clamped_from = "fable"`. `atomic-executor` and `pr-author` C3 cells stay `opus` under every policy.
4. **Emit a routing receipt.** Record a `model_routing_receipts[]` entry `{ agent, phase, complexity_band, fable_policy, table_model, clamped_from | null, model }`. `table_model` is the pre-clamp lookup; `model` is the post-clamp result.

5. **Delegate with the resolved model.** Pass `model` equal to the receipt's `model` on the `Agent(...)` spawn call for that delegation. The orchestrator MUST NOT omit `model` on the spawn (an omitted `model` falls back to the delegate's frontmatter default — `opus` for most workers — which suppresses a `fable` or `sonnet` resolution), and MUST NOT hard-code `model=opus` in a way that overrides the resolved routing model. This applies to every fresh delegation, mirroring the resume-path rule ("delegate with `model` equal to the receipt's `model`") so both paths bind the spawn model to the routing receipt.

The `complexity_assessments[]` and `model_routing_receipts[]` invariants are enforced by `scripts/dev_tools/validate_orchestrator_state.py` per `.claude/rules/orchestrator-state.md`; both arrays remain additive (a checkpoint that predates model routing stays valid).

### Required-once-delegated invariant (`require_model_routing` mode)

The `validate_orchestrator_state_text(...)` validator accepts a `require_model_routing` mode (CLI flag `--require-model-routing`; MCP parameter `require_model_routing`). Under this mode the arrays stop being merely optional: once the checkpoint records at least one delegation (a well-formed `delegation_receipts[]` entry, or a `next_step` that names a delegating agent), every delegated agent must have a matching `model_routing_receipts[]` entry, each matched receipt's phase must have a `complexity_assessments[]` entry, and every present receipt/assessment must be consistent with the reference formulas. A delegation-free checkpoint imposes no requirement, so old checkpoints stay valid. The gate is implemented in `scripts/dev_tools/_orchestrator_state_model_routing_gate.py`; it reuses the per-entry validators and never reimplements `compute_complexity_floor` or `resolve_delegation_model`. Two enforcement layers consume it: the completion gate (`.claude/hooks/validate-orchestrator-output.ps1` passes `--require-model-routing` and surfaces failures as `MODEL_ROUTING_BLOCKED:`), and the pre-delegation deterrent (`.claude/hooks/enforce-model-routing-receipt.ps1`, presence-only). The MCP TypeScript surface performs the existence check only (delegated-agent set ⊆ routing-receipt-agent set); the Python validator is authoritative for per-receipt correctness.

**`fork` caveat.** A skill whose frontmatter `context` field holds the value `fork` inherits the parent model and ignores a model override. Model selection therefore applies to agent delegations, not to fork-routed skill invocations.

## PR Authoring (pr-author Handoff)

PR creation and PR body edits are delegated work, not orchestrator work. The orchestrator MUST NOT call `gh pr create` or `gh pr edit --body*` directly from the main thread; the `enforce-pr-author-skill.ps1` PreToolUse hook blocks those commands unless the `--body-file` argument resolves to a canonical `artifacts/pr_body_<N>.md` path with a matching, verified `artifacts/pr_body_<N>.receipt.json`.

The mandatory sequence is:

1. The orchestrator first refreshes the PR-context artifact via `mcp__drm-copilot__collect_pr_context` (or the equivalent context-collection mechanism), which writes `artifacts/pr_context.summary.txt`.
2. The orchestrator runs the orchestrator-state validator (`mcp__drm-copilot__validate_orchestration_artifacts` or the equivalent local CLI call) against `artifacts/orchestration/orchestrator-state.json --require-pr-creation-ready` and records the pass/fail result under a new `pr_author_preflight` field in the checkpoint, alongside `pr_author_receipt`: `{status ("pass"|"fail"), checked_at (ISO-8601 UTC), checkpoint_path, validator_command, output_summary}`. The orchestrator must not delegate to `Agent(pr-author)` when this preflight fails. The validator's full-lifecycle completion flag (`ci_gate`/`pr_gate`/routing-contract receipts) remains reserved for the post-PR/CI completion context (Step S9 / PR Creation Gate condition 6), not this pre-PR-creation preflight, because those values cannot exist before the first `gh pr create` of a branch.
3. The orchestrator then delegates PR creation and any PR body edits to `Agent(pr-author)`. The `pr-author` agent runs the `pr-author` skill to author the body, writes the body file `artifacts/pr_body_<N>.md` and the sibling receipt `artifacts/pr_body_<N>.receipt.json` with the shape `{skill, pr_body_path, number, sha256 (lowercase hex of the body bytes), context_summary_path, created_at (ISO-8601 UTC, strictly newer than `artifacts/pr_context.summary.txt` last-write)}`, issues `gh pr create --body-file artifacts/pr_body_<N>.md` (or `gh pr edit --body-file ...`), and reports the resulting PR URL or PR number.
4. The orchestrator records `pr_author_receipt` in the checkpoint, citing the body-file path and the receipt path that were verified.

`Agent(pr-author)` is the mandatory delegate for PR creation and PR body edits. Direct `gh pr create`/`gh pr edit --body*` from the main thread is prohibited and is blocked by the hook. Before the five receipt checks, the `enforce-pr-author-skill.ps1` PreToolUse hook independently re-validates the orchestrator-state checkpoint against `--require-pr-creation-ready` (via an injectable `$Invoker` subprocess seam) and blocks with `ORCHESTRATOR_STATE_PREFLIGHT_FAILED` when the checkpoint is missing or invalid — this is the local, hook-level enforcement mechanism that closes the bypass path (it runs inside the same hook that already intercepts `gh pr create`/`gh pr edit`, not as a CI check). The PreToolUse hook then verifies the receipt in five ordered checks: canonical body-file path, receipt present, `number` match, `sha256` match against the body bytes, and `created_at` strictly newer than the context summary last-write.

The SHA-256 receipt is a policy-level integrity check, not a cryptographic or security boundary; any actor with `Write(/artifacts/**)` access can replace both the body file and its receipt together with a matching SHA-256. It binds the body bytes to the receipt, prevents accidental bypass, and requires a deliberate, documented act to circumvent.

## Evidence Location Authority

All evidence artifacts produced during orchestration MUST comply with the canonical scheme defined in `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. Evidence MUST be written to `<FEATURE>/evidence/<kind>/` only.

Permitted `artifacts/`-rooted sub-paths (non-evidence orchestration use only):
- `artifacts/orchestration/` — orchestrator state and checkpoints
- `artifacts/pr_context` — PR context artifacts
- `artifacts/reviews/` — review staging artifacts
- `artifacts/status/` — status update artifacts
- `artifacts/python/` — Python coverage and lcov outputs
- `artifacts/pester/` — Pester coverage outputs
- `artifacts/csharp/` — C# coverage outputs

All other `artifacts/` sub-paths (e.g., `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, `artifacts/evidence/`) are FORBIDDEN for evidence output and will be blocked by the `enforce-evidence-locations.ps1` PreToolUse hook.

## GitHub Actions Reusable Workflows

Every new CI gate in this repository ships as a callable reusable workflow named `_<name>.yml` that declares both `on: workflow_call:` and `on: workflow_dispatch:`. Orchestrator workflows (for example `pr-pipeline.yml`) reference these callees via `uses: ./.github/workflows/_<name>.yml` and contain no inline `steps:` of their own. Cross-job filesystem reliance is not implicit; any job that needs to share files with another job must use explicit `actions/upload-artifact` + `actions/download-artifact`. The GitHub Actions reusable-workflow nesting depth cap is 4; this repository uses one level of nesting and does not introduce additional levels without an explicit design review. See `.github/workflows/README.md` for the full per-stage dispatch and branch-protection rename procedure.

## Completion Requirements

The orchestrator must not report completion until:

1. All required artifacts for the selected workflow path are present on disk.
2. All validation gates (toolchain, acceptance criteria, audit artifacts) have passed.
3. The checkpoint file at `artifacts/orchestration/orchestrator-state.json` reflects the completed state.
4. The model-routing gate passes: `.claude/hooks/validate-orchestrator-output.ps1` runs the validator with `--require-model-routing` alongside `--require-complete` and refuses DONE with `MODEL_ROUTING_BLOCKED:` when a recorded delegation lacks a matching `model_routing_receipts[]` / `complexity_assessments[]` entry (see the required-once-delegated invariant under `## Model Selection`).

## Pre-Feature-Review Commit

Before delegating to the `feature-review` subagent, the orchestrator must:

1. Stage all modified and new files: `git add -A`.
2. Delegate to `Agent(commit-message)` to generate a conventional commit message from the staged diff. The agent is read-only and returns message text only; it does not commit.
3. Commit using the generated message: `git commit -m "<generated message>"`. The `git add` and `git commit` actions remain on the orchestrator.
4. Only after a successful commit may the orchestrator proceed to the `feature-review` delegation.

The review subagent compares against a base branch; uncommitted changes are invisible to the diff tool and cannot be audited.

## Post-Review Outcome Evaluation

After each `feature-review` delegation returns:

1. Locate `remediation-inputs.<timestamp>.md` in the active feature folder (match the highest ISO-8601 timestamp).
2. If no such file exists, treat as zero blocking findings and advance to the PR creation gate.
3. If the file exists, count lines matching `BLOCKING` or `Severity: Blocking` (case-sensitive). If count >= 1, enter the remediation loop. If count = 0, advance to the PR creation gate.

## Remediation Loop (R1–R5)

A bounded loop consisting of five steps. The loop variable `remediation_pass` starts at 1 and increments at R5 before returning to R1.

- **R1 — Remediation planning:** Delegate to `atomic-planner` with `remediation-inputs.<timestamp>.md` path as primary context. Receive `remediation-plan.<timestamp>.md` in the active feature folder.
- **R2 — Preflight clearance:** Delegate to `atomic-executor` for precondition validation only (no implementation). If the executor does not return `PREFLIGHT: ALL CLEAR`, return to R1 and re-delegate to `atomic-planner` with the required-changes output from the executor. Only after `PREFLIGHT: ALL CLEAR` may the orchestrator advance to R3.
- **R3 — Remediation execution:** Delegate to `atomic-executor` with full execution authorization. Each task's toolchain loop (format → lint → type-check → test) is mandatory; no skipping.
- **Pre-R4 commit:** Stage all changes (`git add -A`), delegate to `Agent(commit-message)` to generate a commit message from the staged diff (the agent returns message text only and does not commit), then commit with the generated message. The `git commit` action remains on the orchestrator. Advance to R4 only after a successful commit.
- **R4 — Re-audit:** Delegate to `feature-review` with the same inputs as the original review (resolved base branch, feature folder, refreshed PR context artifacts, acceptance-criteria source). No scope narrowing. The canonical issue number line must be included.
- **R5 — Loop-exit decision:** If the re-audit produces zero blocking findings, exit the loop and advance to the PR creation gate. Otherwise, record `remediation_pass` increment in the checkpoint and return to R1.

**Termination guard:** If `remediation_pass` reaches 3 without resolution, the orchestrator records `step6_status: "blocked_remediation_loop_limit"` in the checkpoint and halts. No further automation is attempted.

## Issue Number Consistency

The canonical issue number is derived once from the active feature folder name: extract the trailing integer from the folder base name (e.g., `2026-04-26-push-down-claude-customizations-162` yields `162`). Record as `issue_num` in the checkpoint.

Every delegation prompt to `atomic-planner`, `atomic-executor`, and `feature-review` must include the line:

> `Canonical issue number for this feature is <issue_num>. All artifact content, file paths, and cross-references must use this number.`

If a subagent artifact references a different issue number, the orchestrator rejects it, requests correction, and records the discrepancy under `artifact_errors` in the checkpoint.

## Step S9 — CI Green Gate

`S9_ci_green` runs after `S8_create_pr` and before any DONE transition. It is the structural guarantee that the orchestrator observes what GitHub Actions produces against the live PR head SHA before writing DONE. S9 applies to every feature, not only features that modify CI paths.

S9 procedure:

1. Resolve the live PR head SHA for the feature branch (`gh pr view --json headRefOid` or equivalent).
2. Invoke `gh pr checks --required --json bucket,name,state,link,workflow` (or an equivalent JSON-emitting command) against that head SHA. `gh` is the only sanctioned channel for querying GitHub Actions state.
3. Parse the JSON via `scripts/orchestration/Invoke-CiGateParser.ps1`, which emits the `ci_gate` object defined below and derives `ci_gate.conclusion` as `success` when all required checks pass, `failure` when any required check failed, and `pending` when any required check is still in progress.
4. Poll with a bounded interval and a documented total timeout while `conclusion == "pending"`. When the timeout is exhausted, set `step9_status: "failed_remediation_required"` and enter the remediation-loop CI-failure handling below with a timeout log.
5. Write the `ci_gate` object and `last_verified_ci_sha` to the checkpoint, and set `step9_status` to `passed` only when `ci_gate.conclusion == "success"` AND `ci_gate.head_sha` equals the current PR head SHA.
6. If the checkpoint's `epic_mode` is `true`, execute `gh pr merge --merge <PR>` merging the feature branch into `epic_context.integration_branch` (already the PR's base branch per the epic-mode `--base` override applied at S8). On success, record `epic_merge: { merge_commit_sha, target_branch, merged_at }` in the checkpoint. On failure due to merge conflict (non-mergeable PR), do not retry blindly: convert the conflict into a synthetic Blocking finding per "Merge-Conflict Remediation" below and re-enter the standard R1–R5 remediation loop; do not proceed to DONE.

DONE is not written while `step9_status` is anything other than `passed`.

## Checkpoint Schema — CI Gate Fields

The orchestrator checkpoint (`artifacts/orchestration/orchestrator-state.json`) is extended with:

- a top-level `ci_gate` object containing:
  - `head_sha` — the PR head SHA that the required checks were observed against.
  - `pr_pipeline_run_id` — the GitHub Actions run id for the PR Pipeline.
  - `pr_pipeline_run_url` — the URL of that run.
  - `conclusion` — one of `success`, `failure`, `pending`.
  - `verified_at` — ISO-8601 timestamp of when S9 recorded the result.
- a top-level `last_verified_ci_sha` — the most recent head SHA for which S9 recorded a result.
- a top-level `step9_status` — an enumeration with at minimum the values `pending`, `passed`, `failed_remediation_required`, and `blocked_ci_loop_limit`.
- a top-level `epic_merge` object (populated only in epic mode) containing:
  - `merge_commit_sha` — the merge commit SHA produced by merging the feature branch into `epic_context.integration_branch`.
  - `target_branch` — the integration branch the feature branch was merged into.
  - `merged_at` — ISO-8601 timestamp of when S9 step 6 recorded the merge.

Illustrative shape:

```jsonc
{
  "completed_steps": ["...", "S8_create_pr", "S9_ci_green"],
  "step9_status": "pending|passed|failed_remediation_required|blocked_ci_loop_limit",
  "ci_gate": {
    "head_sha": "<sha>",
    "pr_pipeline_run_id": "<id>",
    "pr_pipeline_run_url": "<url>",
    "conclusion": "success",
    "verified_at": "<iso8601>"
  },
  "last_verified_ci_sha": "<sha>"
}
```

### Backward compatibility

A checkpoint that predates this schema and has no `ci_gate` object (or no `step9_status`) is treated as `step9_status: "pending"`. Missing CI-gate fields are never interpreted as `passed`; the gate fails closed. The orchestrator runs S9 to populate the fields before any DONE transition.

## Remediation Loop — CI-Failure Handling

When S9 records `step9_status: "failed_remediation_required"` (a failed required check or an exhausted poll timeout):

1. The failed-check log from `gh run view <run-id> --log-failed` (or the timeout log) is written as `remediation-inputs.<timestamp>.md` in the active feature folder.
2. The failure is converted to a synthetic finding with severity `Blocking` that identifies the failing check by name and the failing job by URL.
3. The existing R1-R5 remediation loop processes that finding exactly as it processes a local blocking finding. No new loop is introduced.
4. The `remediation_pass` counter is shared with local-finding passes; the cap is 3.
5. On the third CI-failure pass without resolution, the orchestrator records `step9_status: "blocked_ci_loop_limit"`, does not write DONE, and halts. No further automation is attempted.

## PR Creation Gate

The orchestrator must not create a PR, push a branch for PR purposes, or report work complete until all six conditions are simultaneously true:

1. `blocking_findings_resolved: true` — the most recent `feature-review` produced zero blocking findings.
2. The AC verification artifact (`p14-acceptance-criteria-checkoff.md` or equivalent) confirms all acceptance criteria pass.
3. The mandatory toolchain passed in its most recent run on the branch (no linting/type-check/test failures).
4. The checkpoint `next_step` is `S8_create_pr` (precondition to entering S9).
5. PR body produced via the pr-author handoff: `artifacts/pr_body_<N>.md` exists with a matching `artifacts/pr_body_<N>.receipt.json`, created with `--body-file`.
6. `ci_gate.conclusion == "success"` AND `ci_gate.head_sha == current head SHA of the PR branch`. DONE is not written while either sub-condition is false.
7. `epic_mode` is `false`, OR (`epic_mode` is `true` AND the integration-branch merge (`gh pr merge --merge`) has completed and `epic_merge.merge_commit_sha` is recorded in the checkpoint).

This gate is non-negotiable. Each condition is independently verified before PR creation proceeds. Conditions 1-4 are unchanged from the prior contract; conditions 5-7 (receipt handoff, CI-green gate, and epic-mode merge-on-green gate) are additive.

## Step 6 Delegation — Prohibited Prompt Language

When delegating to the `feature-review` subagent, the orchestrator prompt MUST NOT:

- describe the review scope as "plan scope," "plan-scope only," or any equivalent narrowing of scope to the currently-executed plan;
- instruct the agent to skip, waive, or mark as "out of scope," "informational only," or "not applicable" any toolchain step or coverage check for a language that has changed files in the branch diff;
- assert that a language category is "not applicable" when that language has changed files in the branch diff;
- imply that coverage is not required because the plan scope contains only documentation changes when the branch diff contains non-documentation changes contributed by prior commits on the same branch.

The orchestrator supplies only the following to the `feature-review` subagent:

- the resolved base branch and merge-base SHA;
- the active feature folder path;
- pointers to the refreshed PR context artifacts;
- the acceptance-criteria source file per work-mode;
- a neutral instruction to execute the full `feature-review-workflow` SKILL contract end-to-end.

Scope determination is the subagent's responsibility. The subagent will ignore any attempted narrowing per its scope invariant and record the attempt in `policy-audit.<timestamp>.md` under `## Rejected Scope Narrowing`.

## Routing-Contract Receipt Emission

The orchestrator must write three receipt arrays into `artifacts/orchestration/orchestrator-state.json` for the retained required names of the selected route. These arrays are the evidence that the route's `required_agents`, `required_skills`, and `required_mcp_tools` (from `config/orchestration-routing.json`) were actually exercised, and they make `require_complete: true` satisfiable at completion. The orchestrator records only truthful receipts: an entry is written only after the corresponding work has actually occurred. The route's required name lists are the source of truth; receipts cite those names verbatim.

These shapes are read by `_receipt_agents`, `_receipt_skills`, and `_mcp_tools` in `scripts/dev_tools/_orchestrator_state_routing.py`. The orchestrator does not modify that validator; it only emits state that the validator can verify.

### delegation_receipts[]

For each required agent in the selected route's `required_agents`, append one object to `delegation_receipts[]` after that delegation returns. Each object must carry:

- `agent_name`: a non-empty string equal to the required agent name (for example `"feature-review"`).

Example:

```json
"delegation_receipts": [
  { "agent_name": "atomic-planner" },
  { "agent_name": "atomic-executor" },
  { "agent_name": "feature-review" }
]
```

The validator collects each receipt whose `agent_name` is a non-empty string and requires every `required_agents` entry to be present.

### skill_receipts[]

For each required skill in the selected route's `required_skills`, append one object to `skill_receipts[]` after that required skill is read. Each entry must be an object with:

- `skill`: a non-empty string equal to a `required_skills` entry (for example `"orchestrate"`).
- `required`: the literal boolean `true`.
- `evidence`: a non-empty string, for example `"read:.claude/skills/orchestrate/SKILL.md"`.

Example:

```json
"skill_receipts": [
  { "skill": "orchestrate", "required": true, "evidence": "read:.claude/skills/orchestrate/SKILL.md" }
]
```

The validator counts a skill as acknowledged only when `skill` is a non-empty string, `required` is exactly `true`, and `evidence` is a non-empty string. Every `required_skills` entry must have such a receipt.

### mcp_call_receipts[]

For each required MCP tool in the selected route's `required_mcp_tools`, append one object to `mcp_call_receipts[]` after each successful required MCP call. Each entry must be an object with:

- `tool`: a non-empty string equal to a `required_mcp_tools` entry (for example `"validate_orchestration_artifacts"`).
- `ok`: the literal boolean `true`.
- `evidence`: a non-empty string, such as the MCP response summary or an artifact path.

Example:

```json
"mcp_call_receipts": [
  { "tool": "validate_orchestration_artifacts", "ok": true, "evidence": "plan validator exit 0" }
]
```

The validator counts an MCP receipt as successful only when `tool` is a non-empty string, `ok` is exactly `true`, and `evidence` is a non-empty string. Every `required_mcp_tools` entry must have such a receipt.

These three receipt arrays, populated with the retained required names of the selected route, are what allow the routing-contract validation under `require_complete: true` to pass.
