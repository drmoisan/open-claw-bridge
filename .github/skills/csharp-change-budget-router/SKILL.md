---
name: csharp-change-budget-router
description: Budget-first routing contract for C# work: estimate production-file scope, choose small vs large path, enforce orchestration-first routing for larger changes, and use the VS Code extension command surface for promotion lifecycle steps when available.
---

# C# Change Budget Router

Canonical guidance for deciding whether work should stay on the small path or escalate to the full C# orchestration workflow.

## When to Use This Skill

Use this skill when:
- Intake starts from a natural-language C# request.
- An agent must decide the execution path before planning or implementation.
- A direct-mode route must reject over-budget requests and switch to orchestrated flow.

## Canonical Routing Rules

1) Estimate rough change budget first based on likely **production C# files** touched.
2) Route:
- `1-3` production files (+ corresponding tests) → **small path** (`csharp-typed-engineer` direct mode).
- `>3` production files or `>3` test files → **large path** (orchestration workflow with promotion/research/spec/planning/execution/review).

## Orchestrated Small-Path Requirements

When routed through `csharp-orchestrator`, small path still requires lifecycle scaffolding before implementation:
- invoke promotion/folder lifecycle steps through `vscode/runCommand` + extension access per `feature-promotion-lifecycle` when available; use script/CLI fallback only when direct extension command execution is unavailable,
- promote potential item to GitHub issue with `--work-mode minor-audit`,
- create active feature folder with `--work-mode minor-audit`,
- delegate minimal-audit plan creation to `atomic_planner` with `DIRECTIVE: MINIMAL-AUDIT PLAN REQUIRED`,
- require `atomic_executor` preflight until `PREFLIGHT: ALL CLEAR`,
- execute Phase 0 only via atomic_executor before branching,
- run reduced small-audit after implementation and QC.

Direct invocation of `csharp-typed-engineer` remains implementation-focused and does not replace orchestrator lifecycle steps.

## Direct-Mode Rejection Rule

If direct implementation is requested but estimated scope is `>3` production files:
- Stop before implementation.
- Return explicit routing instruction to invoke `csharp-orchestrator` (or `.github/prompts/orchestrate-csharp-work.prompt.md`).

## Documentation Expectations

Record in response/logs:
- estimated production file count,
- chosen path (`small`/`large`),
- rationale summary (1-3 bullets).
