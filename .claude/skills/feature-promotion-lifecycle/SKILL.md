---
name: feature-promotion-lifecycle
description: Deterministic promotion workflow from potential feature/bug entry to issue, branch, active feature folder, and downstream spec/research handoffs. Agent sessions must use the drm-copilot MCP tool surface and record raw promotion receipts under the canonical checkpoint namespace.
---

# Feature Promotion Lifecycle

Canonical variable model and MCP-only command sequence for promoting potential feature/bug entries and initializing active feature delivery.

## When to Use This Skill

Use this skill when:
- A large-scope change requires feature/bug promotion workflow.
- A short-path workflow still requires promotion/folder initialization before delegated implementation.
- An orchestrator must create potential docs, promote to issue, branch, and active feature folder.
- Downstream research/spec agents depend on deterministic paths and identifiers.

## MCP Tool Availability Preflight

Before any promotion step starts, verify that the required `drm-copilot` MCP tools are available in the current agent session.

Required MCP tool set:
- feature potential entry: `mcp__drm-copilot__new_potential_entry` with `short_name=${short-name}`
- bug potential entry: `mcp__drm-copilot__new_potential_bug_entry` with `short_name=${short-name}`
- potential-to-issue promotion: `mcp__drm-copilot__potential_to_issue` with `potential_path=${relativeFile}`, `promotion_type=${promotion-type}`, `work_mode=${work-mode}`
- active feature folder creation: `mcp__drm-copilot__new_active_feature_folder` with `feature_name=${long-name}`, `type=${promotion-type}`, `issue_number=${issue-num}`, `work_mode=${work-mode}`

If the required MCP tools are unavailable, stop before potential-entry creation, issue promotion, or active-folder creation begins. Restore MCP connectivity first. Agent sessions do not have an approved non-MCP execution branch for promotion work.

## Agent-Session Promotion Execution Rule

Execute the lifecycle only through the MCP tool forms listed above. The MCP path is the sole authoritative execution path for agent sessions.

After each successful promotion operation, persist the raw MCP receipt payload under the matching checkpoint key in `artifacts/orchestration/orchestrator-state.json`:
- `delegation_receipts.promotion.potential_entry`
- `delegation_receipts.promotion.issue`
- `delegation_receipts.promotion.feature_folder`

Each `delegation_receipts.promotion.*` field stores the raw MCP receipt payload returned by the corresponding promotion operation without lossy normalization.

Note: VS Code command-palette commands may exist for interactive extension use, but this note is non-authoritative for agent sessions.

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

When orchestrator routing selects short path, promotion/folder initialization still occurs and MUST use `minor-audit` mode.

1) Use the same MCP tool-availability preflight described above and continue only when the required promotion tools are available.

2) Promote the potential document through `mcp__drm-copilot__potential_to_issue` with `work_mode=minor-audit`.

3) Create branch:
- `${promotion-type}/${short-name}-${issue-num}`

4) Create the active feature folder through `mcp__drm-copilot__new_active_feature_folder` with `work_mode=minor-audit`.

4a) Verify minor-audit folder integrity before proceeding:
- `${feature-folder}/issue.md` exists and contains `- Work Mode: minor-audit`
- `${feature-folder}/issue.md` contains an explicit `## Acceptance Criteria` section
- `${feature-folder}/spec.md` does not exist
- `${feature-folder}/user-story.md` does not exist
- if any check fails, stop and remediate before planning

5) Delegate minimal-audit plan creation to `atomic_planner` with directive:
- `DIRECTIVE: MINIMAL-AUDIT PLAN REQUIRED`

5a) Resolve and persist `${plan-path}` before delegation:
- reuse the earliest existing `plan*.md` in `${feature-folder}` when present
- otherwise create exactly one canonical plan file path and reuse it for all revisions

6) Require preflight validation via `atomic_executor` until:
- `PREFLIGHT: ALL CLEAR`

7) Execute plan Phase 0 only via executor and checkpoint evidence.

8) Branch:
- manual bootstrap: save state and stop,
- non-bootstrap: continue with constrained small-path development.

9) Validate delivery via executor against `issue.md`, then run reduced audit/remediation loop until ready-to-merge.

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
- If a requested `minor-audit` path is rejected by eligibility checks, tooling MUST fail closed to `full-feature`, emit the downgrade reason, and persist `- Work Mode: full-feature`.

