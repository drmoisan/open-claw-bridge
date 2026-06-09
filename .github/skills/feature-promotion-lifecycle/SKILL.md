---
name: feature-promotion-lifecycle
description: Deterministic promotion workflow from potential feature/bug entry to issue, branch, active feature folder, and downstream spec/research handoffs. Prefer VS Code extension command execution when extension tools are available; use underlying scripts only as fallback.
---

# Feature Promotion Lifecycle

Canonical variable model and command sequence for promoting potential feature/bug entries and initializing active feature delivery.

## When to Use This Skill

Use this skill when:
- A large-scope change requires feature/bug promotion workflow.
- A short-path workflow still requires promotion/folder initialization before delegated implementation.
- An orchestrator must create potential docs, promote to issue, branch, and active feature folder.
- Downstream research/spec agents depend on deterministic paths and identifiers.

## Extension-First Execution Rule

When the agent has access to the VS Code extension tool surface (in particular `vscode/runCommand` plus extension access), execute the lifecycle through the contributed extension commands first.

Canonical extension command invocations:
- feature potential entry: `drmCopilotExtension.newPotentialEntry` with `[`"-ShortName"`, `"${short-name}"`]`
- bug potential entry: `drmCopilotExtension.newPotentialBugEntry` with `[`"--short-name"`, `"${short-name}"`]`
- potential-to-issue promotion: `drmCopilotExtension.potentialToIssue` with `[`"--potential-path"`, `"${relativeFile}"`, `"--promotion-type"`, `"${promotion-type}"`, `"--work-mode"`, `"${work-mode}"`]`
- active feature folder creation: `drmCopilotExtension.newActiveFeatureFolder` with `[`"--feature-name"`, `"${long-name}"`, `"--type"`, `"${promotion-type}"`, `"--issue-number"`, `"${issue-num}"`, `"--work-mode"`, `"${work-mode}"`]`

Fallback rule:
- Use the direct script/CLI commands below only when the agent host cannot invoke VS Code extension commands directly.
- When falling back, preserve the same variable model, flags, and work-mode semantics.

## Canonical Variables

- `${promotion-type}`: `feature` or `bug`
- `${short-name}`: lowercase slug, hyphen-separated
- `${relativeFile}`: workspace-relative path to created potential entry markdown
- `${long-name}`: `${relativeFile}` filename without `.md`
- `${issue-num}`: promoted GitHub issue number
- `${feature-folder}`: active feature folder path
- `${plan-path}`: single canonical plan file path reused across planning and preflight revisions
- `${work-mode}`: `minor-audit`, `full-feature`, or `full-bug` (legacy `full` is accepted only as an alias for `full-feature`)
- `${short-path-flag}`: `--work-mode minor-audit` (mandatory for short-path promotion/folder creation)

## Canonical Fallback Command Sequence

1) Create potential entry by type:
- feature: `VS Code command: `drm-copilot: New Potential Entry` (command ID: `drmCopilotExtension.newPotentialEntry`) -ShortName ${short-name}`
- bug: `VS Code command: `drm-copilot: New Potential Bug Entry` (command ID: `drmCopilotExtension.newPotentialBugEntry`) --short-name ${short-name}`

2) Promote potential doc:
- `VS Code command: `drm-copilot: Potential To Issue` (command ID: `drmCopilotExtension.potentialToIssue`) --potential-path ${relativeFile} --promotion-type ${promotion-type} --work-mode ${work-mode}`

3) Create branch:
- `${promotion-type}/${short-name}-${issue-num}`

4) Create active feature folder:
- `VS Code command: `drm-copilot: New Active Feature Folder` (command ID: `drmCopilotExtension.newActiveFeatureFolder`) --feature-name ${long-name} --type ${promotion-type} --issue-number ${issue-num} --work-mode ${work-mode}`

## Canonical Fallback Short-Path Sequence (Minor Audit Mode)

When orchestrator routing selects short path, promotion/folder initialization still occurs and MUST use `minor-audit` mode.

1) Promote potential doc with short-path flag:
- `VS Code command: `drm-copilot: Potential To Issue` (command ID: `drmCopilotExtension.potentialToIssue`) --potential-path ${relativeFile} --promotion-type ${promotion-type} --work-mode minor-audit`

2) Create branch:
- `${promotion-type}/${short-name}-${issue-num}`

3) Create active feature folder with short-path flag:
- `VS Code command: `drm-copilot: New Active Feature Folder` (command ID: `drmCopilotExtension.newActiveFeatureFolder`) --feature-name ${long-name} --type ${promotion-type} --issue-number ${issue-num} --work-mode minor-audit`

3a) Verify minor-audit folder integrity before proceeding:
- `${feature-folder}/issue.md` exists and contains `- Work Mode: minor-audit`
- `${feature-folder}/issue.md` contains an explicit `## Acceptance Criteria` section
- `${feature-folder}/spec.md` does not exist
- `${feature-folder}/user-story.md` does not exist
- if any check fails, stop and remediate before planning

4) Delegate minimal-audit plan creation to `atomic_planner` with directive:
- `DIRECTIVE: MINIMAL-AUDIT PLAN REQUIRED`

4a) Resolve and persist `${plan-path}` before delegation:
- reuse the earliest existing `plan*.md` in `${feature-folder}` when present
- otherwise create exactly one canonical plan file path and reuse it for all revisions

5) Require preflight validation via `atomic_executor` until:
- `PREFLIGHT: ALL CLEAR`

6) Execute plan Phase 0 only via executor and checkpoint evidence.

7) Branch:
- manual bootstrap: save state and stop,
- non-bootstrap: continue with constrained small-path development.

8) Validate delivery via executor against `issue.md`, then run reduced audit/remediation loop until ready-to-merge.

## Required Outputs for Downstream Handoffs

Before delegating research/spec/planning, provide:
- `${feature-folder}/issue.md`
- `${feature-folder}/spec.md` (or expected target path)
- `${feature-folder}/user-story.md` (or explicit `NONE`)
- latest research artifact path(s)
- constraints/APIs/invariants to preserve

Mode-aware expectations:
- For `minor-audit`, the explicit `## Acceptance Criteria` section in `issue.md` is the primary acceptance-criteria source and `spec.md`/`user-story.md` may be intentionally absent by design.
- For `minor-audit`, do not infer acceptance criteria from other `issue.md` sections such as verification notes, next steps, or severity checklists.
- For `minor-audit`, `spec.md`/`user-story.md` must be treated as integrity failures when they appear unexpectedly in the active folder.
- For `full-feature`, `spec.md` and `user-story.md` are expected alongside `issue.md`.
- For `full-bug`, `spec.md` is expected alongside `issue.md`; `user-story.md` should be absent unless the requirements explicitly justify it.

Selected-mode persistence requirements:
- Producer outputs MUST persist exactly one marker in `issue.md` metadata above the first `##` heading:
	- `- Work Mode: minor-audit`
	- `- Work Mode: full-feature`
	- `- Work Mode: full-bug`
- Persisted marker MUST represent selected mode after eligibility checks, not requested mode.
- If a legacy requested `full` path is accepted, tooling MUST normalize it to `full-feature` before persistence.
- If a requested `minor-audit` path is rejected by eligibility checks, tooling MUST fail closed to `full-feature`, emit fallback reason, and persist `- Work Mode: full-feature`.
