---
name: powershell-qa-gate
description: Final QA gate for PowerShell changes. Executes the full PoshQC format -> analyze -> test toolchain (with coverage where enforced), compares against a captured baseline, enforces zero-regression deltas, and produces the required reporting block before the agent declares the change complete.
---

# PowerShell QA Gate

Canonical procedure for the Phase D final quality gate that every PowerShell change must pass before completion is reported.

## When to Use This Skill

Use this skill when:

- `powershell-typed-engineer` is about to declare a change complete.
- An executor has finished applying a planned batch and must verify zero regressions against the baseline captured in Phase A.
- A reviewer needs to confirm the toolchain was actually run and produced a clean pass.

## Required Inputs

Before invoking this gate, the agent must have:

- a baseline record produced in Phase A, containing PSScriptAnalyzer findings, Pester pass/fail status, and per-file coverage status for the in-scope files,
- the exact list of touched production and test files,
- a clean working tree (all planned edits committed to the working copy).

## Toolchain Execution Sequence

Run the full toolchain in this exact order. If any step fails or modifies files, fix the issue and restart from step 1. Do not stop the loop until all steps complete without errors in a single pass.

1. `mcp__drm-copilot__run_poshqc_format`
2. `mcp__drm-copilot__run_poshqc_analyze`
3. `mcp__drm-copilot__run_poshqc_test`
4. Coverage check (when enforced by task/repo flow) — use the Pester coverage output from step 3 or the repo coverage task.

If the environment prevents running any tool, stop and report the change as **unverified**. Do not declare completion.

## Delta Requirements (Zero-Regression Hard Gate)

Compare the final results to the Phase A baseline. All of the following must hold:

- **PSScriptAnalyzer delta**: 0 new findings across the repository.
- **Pester delta**: 0 new failing tests.
- **Per-file coverage delta**: coverage for every touched file is greater than or equal to the baseline for that file.
- **Overall coverage delta** (when the repo enforces it): overall coverage is greater than or equal to the baseline.
- **New modules, classes, or methods**: line coverage >= 85% and branch coverage >= 75% per the uniform tier rule (`.claude/rules/quality-tiers.md`). No tier-specific lower thresholds. No regression on changed lines.

If any delta check fails, the agent must revert or fix immediately and rerun the full toolchain. Do not proceed to reporting until all deltas are clean.

## Required Reporting Block

Every completion response must include the following sections:

1. **Scope** — exact file list touched in this change.
2. **Baseline** — PSScriptAnalyzer, Pester, and coverage status recorded in Phase A.
3. **Plan** — design and test-strategy summary, referencing the approved plan, including proposed script/module structure, minimal DI seams, Pester scenario-level test strategy, and external executable wrapper mock strategy.
4. **Diffs** — patch-style or full-file replacements for scoped files only.
5. **QA Gate Results** — PSScriptAnalyzer delta, failing-tests delta, per-file coverage delta, and overall coverage delta when applicable. If any step could not be run, mark the corresponding line **unverified** and state why.

## Evidence Storage

Persist toolchain output according to `evidence-and-timestamp-conventions`:

- store baseline outputs under `<FEATURE>/evidence/baseline/<timestamp>/`,
- store post-change outputs under `<FEATURE>/evidence/qa-gates/<timestamp>/`,
- use ISO-8601 UTC timestamps in folder names.

This location is canonical per evidence-and-timestamp-conventions and is not overridable.
See `.claude/skills/evidence-and-timestamp-conventions/SKILL.md` for the canonical evidence path authority.

The evidence paths must be referenced in the agent's completion message to satisfy the `SubagentStop` completion-artifact gate.

## Prohibited Shortcuts

- Do not disable, skip, or narrow any step of the toolchain to reach a clean result.
- Do not add PSScriptAnalyzer rule suppressions (`[Diagnostics.CodeAnalysis.SuppressMessageAttribute]` or `PSScriptAnalyzer` suppression comments) or Pester `-Skip` / `-Pending` to mask new findings introduced by the change.
- Do not report success based on partial or targeted runs alone. Targeted runs are allowed mid-batch, but the final gate requires a full-repo pass.
- Do not mock `git`, `gh`, `actionlint`, or other executables directly; mock the wrapper function (for example `Invoke-GitExe`) per `.claude/rules/powershell.md`.
