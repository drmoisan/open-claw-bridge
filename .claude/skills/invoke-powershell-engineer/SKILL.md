---
name: invoke-powershell-engineer
description: Invoke the powershell-typed-engineer worker to design, implement, and verify PowerShell changes within typed repository boundaries. Applies PoshQC format -> analyze -> test toolchain, the 1-2 production-file direct-mode budget, the 3-production + 3-test per-batch cap, and zero-regression quality gates.
---

# Implement PowerShell Skill

This direct-use wrapper delegates PowerShell implementation work to the `powershell-typed-engineer` worker. Use this entry point when a prompt needs a scoped PowerShell change that must stay inside the typed engineer's guardrails.

## When to Use This Skill

Use this skill when:

- The user requests a PowerShell code change, bug fix, refactor, or test addition.
- Estimated scope fits the direct-mode path (1-2 production PowerShell files plus corresponding tests).
- The toolchain (PoshQC format, PSScriptAnalyzer, Pester with coverage where enforced) can be run in the current environment, or the user has explicitly authorized an unverified plan-only response.

If the estimated scope exceeds the direct-mode budget, this skill defers to the orchestrated flow via `powershell-change-budget-router` instead of proceeding directly.

## Inputs

- Objective statement (what the change must accomplish).
- Files or entrypoints in scope (exact script or module paths and corresponding `*.Tests.ps1` paths).
- Constraints, including public function or module contracts that must be preserved.
- Optional approved plan. If none is supplied, the worker delegates plan authoring to `atomic_planner` before any edits.
- Optional budget override in the form `budget: prod=<N>, test=<M>` subject to repo policy compliance.

## Output Paths

- PowerShell source (`*.ps1`, `*.psm1`, `*.psd1`) and Pester test (`*.Tests.ps1`) files within the approved scope.
- Baseline evidence under `<FEATURE>/evidence/baseline/<timestamp>/` and post-change evidence under `<FEATURE>/evidence/qa-gates/<timestamp>/` per `evidence-and-timestamp-conventions`.
- This location is canonical per evidence-and-timestamp-conventions and is not overridable. See `.claude/skills/evidence-and-timestamp-conventions/SKILL.md` for the canonical evidence path authority.
- Plan artifacts under the active feature folder when the task is feature-scoped.

## Required Reporting Block

The worker must return the `powershell-qa-gate` reporting block:

1. Scope (exact file list).
2. Baseline (PSScriptAnalyzer, Pester, coverage status).
3. Plan (design and test strategy, including minimal DI seams and external executable wrapper mock strategy).
4. Diffs (patch-style or full-file replacements).
5. QA Gate Results (PSScriptAnalyzer, Pester, and coverage deltas, or clearly marked **unverified**).

## Worker Routing

- Worker: `powershell-typed-engineer`

## Preloaded Contracts

The worker operates under the following preloaded skills and rules:

- `policy-compliance-order`
- `powershell-change-budget-router`
- `powershell-orchestration-state-machine`
- `atomic-plan-contract`
- `powershell-qa-gate`
- `acceptance-criteria-tracking`
- `feature-promotion-lifecycle`
- `remediation-handoff-atomic-planner`
- `evidence-and-timestamp-conventions`
- `.claude/rules/powershell.md` (path-scoped for `**/*.ps1`, `**/*.psm1`, `**/*.psd1`)
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/tonality.md`
