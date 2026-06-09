---
name: orchestrate
description: Route a repository request through the deterministic orchestration workflow for feature, bug, research, planning, execution, and review handoffs.
argument-hint: "[objective]"
---

# Orchestrate Skill

This skill frames work for the already-active main session, which serves as the orchestrator runtime for end-to-end feature or bug delivery.

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

## Delegation Model

After reading `artifacts/orchestration/orchestrator-state.json`, the main session delegates work exclusively through configured workers:

- `atomic-planner` — generates phased implementation plans
- `atomic-executor` — executes approved plans task-by-task
- `feature-review` — produces policy, code, and feature audit artifacts
- `task-researcher` — performs deep research and writes findings to `artifacts/research/`

The orchestrator does not perform deep implementation itself. It coordinates, tracks state, and enforces completion.

## Evidence Location Authority

All evidence artifacts produced during orchestration MUST comply with the canonical scheme defined in `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. Evidence MUST be written to `<FEATURE>/evidence/<kind>/` only.

Permitted `artifacts/`-rooted sub-paths (non-evidence orchestration use only):
- `artifacts/orchestration/` — orchestrator state and checkpoints
- `artifacts/research/` — research outputs from task-researcher
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

## Pre-Feature-Review Commit

Before delegating to the `feature-review` subagent, the orchestrator must:

1. Stage all modified and new files: `git add -A`.
2. Invoke the `commit-message` skill to generate a conventional commit message from the staged diff.
3. Commit using the generated message: `git commit -m "<generated message>"`.
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
- **Pre-R4 commit:** Stage all changes (`git add -A`), invoke the `commit-message` skill to generate a commit message from the staged diff, commit with the generated message. Advance to R4 only after a successful commit.
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

## PR Authoring (pr-author Handoff)

The orchestrator MUST NOT author a PR body itself or pass an ad-hoc body to
`gh`. The PR body is produced exclusively by the `pr-author` skill, and the
`enforce-pr-author-skill.ps1` PreToolUse hook blocks any `gh pr create` /
`gh pr edit --body-file` that bypasses this handoff. The required sequence is:

1. Refresh the PR context bundle: `mcp__drm-copilot__collect_pr_context --base <base>`,
   which writes `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`.
2. Invoke the `pr-author` skill. It reads only the context bundle and returns
   the GitHub-ready PR body **as its message** (per `.claude/skills/pr-author/SKILL.md`).
   Do not hand-write or paraphrase the body; use the returned text verbatim.
3. Write the returned body to the canonical path `artifacts/pr_body_<N>.md`
   (`<N>` = PR or canonical issue number), then write a sibling provenance
   receipt `artifacts/pr_body_<N>.receipt.json` with the shape:

   ```jsonc
   {
     "skill": "pr-author",
     "pr_body_path": "artifacts/pr_body_<N>.md",
     "number": <N>,
     "sha256": "<lowercase hex sha256 of the pr_body file bytes>",
     "context_summary_path": "artifacts/pr_context.summary.txt",
     "created_at": "<ISO-8601 UTC, must be newer than pr_context.summary.txt>"
   }
   ```

   The `sha256` is computed over the bytes of the body file exactly as written.
4. Create the PR with `gh pr create --body-file artifacts/pr_body_<N>.md`
   (or update an existing PR with `gh pr edit <N> --body-file artifacts/pr_body_<N>.md`).

The hook rejects, with a specific reason, any of: an inline `--body`; no body
flag; a `--body-file` path that is not `artifacts/pr_body_<N>.md`; a missing or
stale receipt; a receipt whose `number` does not match the filename; or a
receipt whose `sha256` does not match the body file on disk. Record
`pr_author_receipt: "artifacts/pr_body_<N>.receipt.json"` in the checkpoint once
the PR is created.

## PR Creation Gate

The orchestrator must not create a PR, push a branch for PR purposes, or report work complete until all six conditions are simultaneously true:

1. `blocking_findings_resolved: true` — the most recent `feature-review` produced zero blocking findings.
2. The AC verification artifact (`p14-acceptance-criteria-checkoff.md` or equivalent) confirms all acceptance criteria pass.
3. The mandatory toolchain passed in its most recent run on the branch (no linting/type-check/test failures).
4. The checkpoint `next_step` is `S8_create_pr` (precondition to entering S9).
5. The PR body was produced via the **PR Authoring (pr-author Handoff)** above: `artifacts/pr_body_<N>.md` exists with a matching `artifacts/pr_body_<N>.receipt.json`, and the PR was created with `--body-file` pointing at that file.
6. `ci_gate.conclusion == "success"` AND `ci_gate.head_sha == current head SHA of the PR branch`. DONE is not written while either sub-condition is false.

This gate is non-negotiable. Each condition is independently verified before PR creation proceeds. Conditions 1-4 are unchanged from the prior contract; conditions 5 (pr-author handoff) and 6 (CI green) are additive.

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
