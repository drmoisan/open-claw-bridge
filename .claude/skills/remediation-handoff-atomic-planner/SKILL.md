---
name: remediation-handoff-atomic-planner
description: 'Remediation handoff chain from orchestrator through atomic-planner, atomic-executor, and feature-review. Use when an audit cycle requires a delegated remediation plan, preflight clearance, task-by-task execution, and reaudit.'
---

# Remediation Handoff to atomic-planner

Strict handoff chain for orchestrator-initiated remediation cycles. The chain enforces that an `atomic-planner`-authored plan is preflighted by `atomic-executor`, revised through a preflight sub-loop if needed, executed by `atomic-executor` task-by-task, and reaudited by `feature-review`. Workers are never invoked directly by the orchestrator.

## When to Use This Skill

Use this skill when:

- Audit findings require remediation (FAIL or material PARTIAL findings).
- Toolchain checks fail.
- Acceptance criteria are unmet.
- A required CI check fails after the pull request is open.
- Any new finding surfaces during execution of an active cycle (in which case begin a new cycle, do not extend the active one).

## Full Handoff Chain

```
orchestrator
  -> writes remediation/<entry-ts>/remediation-inputs.md
  -> delegates to atomic-planner
     -> atomic-planner authors remediation/<entry-ts>/remediation-plan.md
        (plan shape per .claude/skills/atomic-plan-contract/SKILL.md)
     -> orchestrator hands plan to atomic-executor for preflight
        -> atomic-executor returns one of:
             PREFLIGHT: ALL CLEAR        -> proceed to execution
             PREFLIGHT: REVISIONS REQUIRED -> route back to atomic-planner
                with a precise plan delta; planner revises in place and
                orchestrator re-runs preflight (sub-loop until clear)
     -> atomic-executor executes the cleared plan task-by-task
        (workers are invoked by atomic-executor only)
     -> orchestrator delegates to feature-review
        -> feature-review writes audit/<exit-ts>/code-review.md,
           audit/<exit-ts>/feature-audit.md, and audit/<exit-ts>/policy-audit.md
  -> orchestrator evaluates exit condition:
     blocking_count == 0 -> mark exit_condition_met = true, end loop
     blocking_count > 0  -> begin cycle N+1 with new remediation/<new-ts>/remediation-inputs.md
```

Every audit (including the initial, non-remediation-triggered `feature-review` pass that first discovers findings) is written under an `audit/<timestamp>/` folder. Every remediation cycle's entry artifacts are written under a `remediation/<timestamp>/` folder. This folder-per-cycle layout is the canonical pattern for multi-cycle remediation: it keeps each cycle's inputs, plan, and reaudit artifacts visually and structurally grouped, rather than relying on filename timestamp suffixes alone to disambiguate cycles sharing a feature folder.

The orchestrator must not call typed-engineer workers directly during any phase of the cycle. The orchestrator must not act on a preflight delta itself; revisions are routed to `atomic-planner`.

## Trigger Conditions

Trigger remediation when any of these are true:

- Audit artifacts contain FAIL or meaningful PARTIAL findings.
- Toolchain checks fail (format, lint, type-check, test, contract, or integration stages).
- Acceptance criteria are not met.
- A required CI check fails after the PR is open (workflow-file changes specifically must enter the loop and trigger the `modified-workflow-needs-green-run` rule defined in `.claude/skills/feature-review-workflow/SKILL.md`).

## Required Remediation Inputs

The orchestrator authors `remediation/<entry-ts>/remediation-inputs.md` with:

- Enumerated fix list with file paths, expected behavior, and verification commands.
- A "do not do" list (no scope creep, no policy weakening, no silent skips).
- A pointer to the audit artifacts that produced the findings.

## Required Artifacts

Each remediation cycle produces exactly five artifacts under the active feature folder, grouped into one `remediation/<entry-ts>/` folder and one `audit/<exit-ts>/` folder:

1. `docs/features/active/<slug>/remediation/<entry-ts>/remediation-inputs.md` — orchestrator authors at cycle entry.
2. `docs/features/active/<slug>/remediation/<entry-ts>/remediation-plan.md` — `atomic-planner` authors at cycle entry.
3. `docs/features/active/<slug>/audit/<exit-ts>/code-review.md` — `feature-review` authors at cycle exit.
4. `docs/features/active/<slug>/audit/<exit-ts>/feature-audit.md` — `feature-review` authors at cycle exit.
5. `docs/features/active/<slug>/audit/<exit-ts>/policy-audit.md` — `feature-review` authors at cycle exit.

The initial, non-remediation-triggered `feature-review` pass (the audit that first discovers findings and opens cycle 1) also writes its three artifacts under `docs/features/active/<slug>/audit/<its-own-timestamp>/`, for the same reason: one timestamped folder per audit event, regardless of whether that audit opens, closes, or does not trigger a remediation cycle.

Timestamp rule:

- `<entry-ts>` is the ISO-8601 timestamp at cycle entry, in the `yyyy-MM-ddTHH-mm` format defined in `.claude/skills/evidence-and-timestamp-conventions/SKILL.md`. It names both the `remediation/<entry-ts>/` folder and, within it, `remediation-inputs.md` and `remediation-plan.md`.
- `<exit-ts>` is the ISO-8601 timestamp at cycle exit (when `feature-review` runs), in the same `yyyy-MM-ddTHH-mm` format. It names the `audit/<exit-ts>/` folder and, within it, all three reaudit artifacts: `code-review.md`, `feature-audit.md`, and `policy-audit.md`.

A cycle with fewer than five artifacts is malformed. A cycle that uses the same timestamp value for both its `remediation/<ts>/` and `audit/<ts>/` folders is malformed unless entry and exit genuinely ran within the same minute — the two folders remain distinct either way, since one is named `remediation/` and the other `audit/`.

## Plan Shape

`remediation/<entry-ts>/remediation-plan.md` MUST conform to `.claude/skills/atomic-plan-contract/SKILL.md`. In particular:

- Phase headings: `### Phase N — <Title>`.
- Tasks: `- [ ] [P#-T#]` with sequential per-phase IDs.
- Phase 0 captures policy reads and baseline toolchain results per `policy-compliance-order` and `evidence-and-timestamp-conventions`.
- Final phase runs the full toolchain QA loop in the order specified by `atomic-plan-contract` and records numeric coverage values where the repository policy requires coverage.
- Evidence paths resolve to `<FEATURE>/evidence/<kind>/` per the non-overridable evidence path clause.

The plan must pass the `mcp__drm-copilot__validate_orchestration_artifacts` MCP tool with `artifact_type: "plan"` and `artifact_path: <plan-path>` before `atomic-executor` runs preflight against it.

## Preflight Sub-Loop

After the plan is authored, `atomic-executor` runs preflight under the directive `DIRECTIVE: PREFLIGHT VALIDATION ONLY`. The exact signal returned is either:

- `PREFLIGHT: ALL CLEAR` — the plan proceeds to execution.
- `PREFLIGHT: REVISIONS REQUIRED` — `atomic-executor` returns a precise plan delta. The orchestrator routes the delta to `atomic-planner` for revision. `atomic-planner` updates the same plan file in place (per the plan-path continuity contract in `atomic-plan-contract`). The orchestrator then re-runs preflight. The sub-loop repeats until `PREFLIGHT: ALL CLEAR` is returned.

The orchestrator records the preflight outcome in `remediation_loop.cycles[current_cycle].preflight` with `iterations` (counter) and `final_status` (`clear|changes_requested|pending`).

## Execution and Reaudit

When preflight is clear, `atomic-executor` executes the plan task-by-task. The executor invokes workers (`python-typed-engineer`, `typescript-engineer`, `csharp-typed-engineer`, `powershell-typed-engineer`) internally as needed. The orchestrator does not call workers.

When execution is complete, the orchestrator delegates to `feature-review`. `feature-review` produces the three reaudit artifacts under `docs/features/active/<slug>/audit/<exit-ts>/` using the exit timestamp.

## Exit Gate

The orchestrator reads the latest cycle's three reaudit artifacts and computes `blocking_count` as the total number of FAIL findings plus material PARTIAL findings flagged as blocking. Only when `blocking_count == 0` does the orchestrator set `exit_condition_met = true` on the current cycle and mark the remediation loop complete. Otherwise, the orchestrator opens cycle N+1 with a new `remediation/<new-ts>/remediation-inputs.md` and runs the full chain again.

## Context Package (When Required)

If the calling agent requires a context package, inline the relevant audit artifacts, PR context artifacts, and any active plan files in the delegated prompt to `atomic-planner` rather than referencing them by path alone.
