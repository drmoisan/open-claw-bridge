---
name: powershell-change-budget-router
description: Budget-first routing contract for PowerShell work: estimate production-file scope, choose small vs large path, enforce direct-mode escalation to orchestrator, and use the VS Code extension command surface for promotion lifecycle steps when available.
---

# PowerShell Change Budget Router

Canonical guidance for deciding whether work should execute directly in `powershell-typed-engineer` or be escalated to `powershell-orchestrator`.

## When to Use This Skill

Use this skill when:
- Intake starts from a natural-language PowerShell request.
- An agent must decide execution path before planning or implementation.
- A direct implementation agent must reject over-budget requests and route to orchestrator.

## Canonical Routing Rules

1) Estimate rough change budget first based on likely **production PowerShell files** touched.
2) Route:
- `1-2` production files (+ corresponding tests) → **small path** (`powershell-typed-engineer` direct mode).
- `>2` production files → **large path** (`powershell-orchestrator`).

## Orchestrated Small-Path Requirements

When routed through `powershell-orchestrator`, small path still requires lifecycle scaffolding before implementation:
- invoke promotion/folder lifecycle steps through `vscode/runCommand` + extension access per `feature-promotion-lifecycle` when available; use script/CLI fallback only when direct extension command execution is unavailable,
- promote potential item to GitHub issue with `--work-mode minor-audit`,
- create active feature folder with `--work-mode minor-audit`,
- delegate minimal-audit plan creation to `atomic_planner` with `DIRECTIVE: MINIMAL-AUDIT PLAN REQUIRED`,
- require `atomic_executor` preflight until `PREFLIGHT: ALL CLEAR`,
- execute Phase 0 only via `atomic_executor` before branching,
- run reduced small-audit after implementation and QC.

Direct invocation of `powershell-typed-engineer` remains implementation-focused and does not replace orchestrator lifecycle steps.

## Direct-Mode Rejection Rule

If `powershell-typed-engineer` is invoked directly and estimated scope is `>2` production files:
- Stop before implementation.
- Return explicit routing instruction to invoke `powershell-orchestrator` (or `.github/prompts/orchestrate-powershell-work.prompt.md`).

## Documentation Expectations

Record in response/logs:
- estimated production file count,
- chosen path (`small`/`large`),
- rationale summary (1-3 bullets).
