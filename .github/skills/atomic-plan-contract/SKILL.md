---
name: atomic-plan-contract
description: 'Atomic plan format and toolchain contract shared by planning and execution agents. Use when generating, validating, or executing atomic plans with Phase 0, baseline capture, and final QA loops.'
---

# Atomic Plan Contract

Shared rules for atomic plan formatting, Phase 0 requirements, baseline capture, and final QA loops.

## When to Use This Skill

Use this skill when:
- Creating or validating atomic plans.
- Executing plans with strict format requirements.
- Enforcing Phase 0 policy reading + baseline capture and final QA loops.

## Canonical Plan Format

- Phase headings must be: `### Phase N — <Title>`
- Tasks must start with: `- [ ] [P#-T#]` (or `[x]` for completed)
- Task IDs must match their phase and be sequential per phase.
- Plans must pass the `validate_orchestration_artifacts` MCP tool with `artifact_type: "plan"` and `artifact_path: <plan-path>` before they can be reported as approved.

## Short-Path Minimal Plan Contract

When orchestration selects short path, a minimal plan is still mandatory and must include these blocks:

1) Baseline capture block
- policy reads in required order,
- baseline toolchain/test state capture for each language that has explicit baseline command tasks in the plan.
- required artifacts:
	- a Phase 0 policy-read evidence artifact in the canonical evidence location defined by `evidence-and-timestamp-conventions`
	- one baseline artifact per baseline command step (no aggregate-only baseline artifact)
	- each baseline step artifact MUST include: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
	- baseline test-step artifacts for languages with mandatory coverage policy MUST include numeric coverage headline values in `Output Summary:` (baseline percent and, when applicable, targeted module/new-code percent).

2) Delegated implementation block
- explicit handoff task to the small-path implementation engineer,
- acceptance criteria for implementation completion.

3) Final QC block
- full language-appropriate QA loop (format → lint → type-check when applicable → test),
- rerun behavior when any step changes files or fails.
- one final-QC artifact per QC command step (no aggregate-only final-QC artifact)
- each final-QC step artifact MUST include: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`
- final-QC test commands for languages with mandatory coverage policy MUST run in coverage mode and record numeric post-change coverage values in `Output Summary:`.
- final-QC command tasks that are present in the approved plan MUST execute their stated commands; `SKIPPED` is invalid unless the task text itself explicitly authorizes a skip condition.

4) Reduced audit block
- explicit post-implementation small-audit handoff,
- reduced artifact checks required by short-path policy.

## Minimal-Audit Directive Contract (Small Path)

For short-path planning handoffs, orchestrators MUST include:
- `DIRECTIVE: MINIMAL-AUDIT PLAN REQUIRED`

Required planner outputs for this directive:
- plan MUST use `${feature-folder}/issue.md` as sole requirements source,
- plan MUST require `${feature-folder}/issue.md` to contain an explicit `## Acceptance Criteria` section and MUST treat only that section as the minor-audit AC source,
- plan MUST NOT require `spec.md`/`user-story.md`/`research.md`,
- plan MUST include exactly 3 phases:
	- Phase 0 baseline capture,
	- Phase 1 placeholder for constrained small-path implementation,
	- Phase 2 final QC loop,
- final-QC command tasks in the generated plan MUST be unconditional when present; no IN_SCOPE/OUT_OF_SCOPE branches and no SKIPPED completion path unless the task text explicitly authorizes a skip branch,
- planner MUST return `plan-path` and final preflight signal.

## Phase-0-Only Execution Contract (Small Path)

After preflight all-clear on the minimal-audit plan:
- orchestrator MUST delegate to executor to run Phase 0 only,
- orchestrator MUST checkpoint Phase 0 evidence before branching.

Branching after Phase 0:
- `manual bootstrap` → save state and stop for manual resume,
- otherwise continue with constrained small-path development, then executor validation, then reduced audit/remediation loop.

## Phase 0 Requirements

Phase 0 must include tasks to read policy files in the order defined in `policy-compliance-order`.

Phase 0 must also capture baseline toolchain results for the languages touched. Baseline artifact conventions (location + required fields) are defined in `evidence-and-timestamp-conventions`.

For short-path/minimal-audit plans, Phase 0 evidence is incomplete unless both artifacts exist:
- `phase0-instructions-read.md` with at least: `Timestamp:`, `Policy Order:`, and explicit list of files read.
- baseline command-step artifacts with at least: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` for each baseline check executed.
- approved-plan checklist state MUST remain unchecked for any Phase 0 task whose artifact is absent or whose artifact fields are incomplete.

`Output Summary:` is mandatory for each command-step artifact and must concisely summarize the essential result signal (for example: pass/fail status, key counts, coverage headline, or primary diagnostic).

## Coverage Evidence Contract (Mandatory when policy requires coverage)

For any language in scope where repository policy requires coverage validation:

- The approved plan MUST include explicit baseline and final-QC coverage capture tasks.
- Baseline and final-QC artifacts MUST record numeric coverage values (not placeholders such as `UNVERIFIED`).
- Where policy requires no-regression and new-code thresholds, the plan MUST include a delta/threshold verification task that reports:
	- baseline coverage,
	- post-change coverage,
	- new/changed-code coverage.
- If required coverage values are unavailable, the plan outcome MUST be remediation-required and MUST NOT be reported as PASS.

## Final QA Loop (Required for Code/Test Changes)

Run the full toolchain loop for each applicable language in order:
1) Formatting
2) Linting
3) Type checking (if applicable)
4) Testing

For languages with mandatory coverage policy, step 4 must use coverage-enabled test commands and persist numeric coverage evidence.

If any step fails or changes files, restart the loop from step 1 until a clean pass completes.

## No-SKIPPED Rule for Planned Command Tasks

For command-bearing tasks in approved plans (especially Phase 2 final-QC tasks):
- if the task exists in the plan, the command must be executed and recorded,
- `EXIT_CODE: SKIPPED` MUST NOT be used as a passing outcome,
- exceptions are allowed only when the task text itself explicitly contains a skip branch and that branch is intentionally approved in requirements.

## Expect-Fail Test Tasks

Any regression test task expected to fail must be tagged with `[expect-fail]` and include an auditable evidence artifact per `evidence-and-timestamp-conventions`.

## Preflight Validation (Planner ↔ Executor)

When validating or handing off plans for execution:
- Use the directive line: `DIRECTIVE: PREFLIGHT VALIDATION ONLY`.
- Require one of the exact signals:
	- `PREFLIGHT: ALL CLEAR`
	- `PREFLIGHT: REVISIONS REQUIRED`
- If revisions are required, provide a precise plan delta and repeat validation until all clear.
- If the required planner ↔ executor handoff cannot be started or completed, stop and report blocked state; do not self-approve the plan.

## Validator Gate (Mandatory)

Before a plan can be treated as approved:

- run the `validate_orchestration_artifacts` MCP tool with `artifact_type: "plan"` and `artifact_path: <plan-path>`,
- reject the plan if that validator exits non-zero,
- do not treat human-readable summaries as a substitute for validator success.

## Plan-Path Continuity Contract (Mandatory)

When a caller provides an explicit target plan file path (for example `${plan-path}` or `${file}`):

- Planner MUST update that exact file in place.
- Planner MUST reuse the same file for all preflight revision iterations.
- Planner MUST NOT create additional timestamped sibling files (for example `plan.<timestamp>.md`) during the same planning cycle.

If the provided path does not exist, it may be created once, then reused for all subsequent revisions in that cycle.

## Mode source precedence (Mandatory)

When a plan is generated or validated from a feature folder, resolve selected mode in this order:

1) Persisted marker in `issue.md` metadata block:
	- `- Work Mode: minor-audit`
	- `- Work Mode: full-feature`
	- `- Work Mode: full-bug`
2) Legacy compatibility marker `- Work Mode: full` resolves to `full-feature`
3) Explicit workflow override only when repo policy allows and only when reconciled against `issue.md`
4) Fail closed to `full-feature` when marker is missing or malformed

## Mode-Specific Mandatory Plan Gates

- `minor-audit` plans MUST include baseline evidence tasks, targeted verification evidence tasks, and end-state evidence tasks.
- `minor-audit` plans MUST require `${feature-folder}/issue.md` to contain an explicit `## Acceptance Criteria` section; do not infer acceptance criteria from other `issue.md` sections.
- `minor-audit` plans MUST NOT treat missing `spec.md` or `user-story.md` as automatic blockers.
- `minor-audit` execution/validation/audit MUST fail closed when `spec.md` or `user-story.md` exists unexpectedly in the active folder, when the explicit `## Acceptance Criteria` section is missing from `issue.md`, when required Phase 0 artifacts are missing, or when checklist state contradicts evidence on disk.
- `full-feature` plans MUST enforce full-document expectations (`spec.md` + `user-story.md`) and full QA loop obligations.
- `full-bug` plans MUST enforce spec-driven expectations (`spec.md` required, `user-story.md` optional/absent by default) and full QA loop obligations.
