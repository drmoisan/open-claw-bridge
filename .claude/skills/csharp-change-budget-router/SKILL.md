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
- Return explicit routing instruction to invoke `csharp-orchestrator`.

## Invocation Mode

The `csharp-typed-engineer` worker supports two execution modes:

- **Direct mode** (default): no handoff directive present. Strict overall change-budget limits apply per the routing rules above.
- **Orchestrator handoff mode**: enabled only when the incoming request contains the exact line `DIRECTIVE: ORCHESTRATOR HANDOFF MODE`. Overall change-budget limits are lifted, but execution is allowed only when a complete context package is supplied.

Required context package in orchestrator handoff mode:

1. objective and expected outcome,
2. `${promotion-type}` and `${issue-num}` when available,
3. `${feature-folder}` path,
4. issue doc path (`${feature-folder}/issue.md`),
5. spec doc path (`${feature-folder}/spec.md`),
6. user-story path (`${feature-folder}/user-story.md`) or explicit `NONE`,
7. research artifact path(s),
8. constraints, APIs, or invariants to preserve.

If any required item is missing in orchestrator handoff mode, stop and request the missing context package fields before Phase A proceeds.

## Per-Batch Cap

In all modes, per-batch budget remains: at most **3 production files** and **3 test files** unless an explicit override is approved by the user. If no override is provided, the 3/3 per-batch limit applies. If a batch would exceed the cap, split it into smaller batches.

## Orchestrator-Mode Delegation Chain

When `csharp-typed-engineer` runs in orchestrator handoff mode, it must execute the following delegation chain and must not bypass it with direct implementation:

1. Delegate to `atomic_planner` for an architecture plus testability plan only (no edits).
2. Require planner output to include final `PREFLIGHT: ALL CLEAR` from the preflight validation loop.
3. Delegate plan execution to `atomic_executor`.
4. Delegate the final QA gate to `atomic_executor` per `csharp-qa-gate`.
5. Delegate post-implementation review to `feature-review` per `feature-review-workflow`.

Execution-start constraint: in orchestrator handoff mode, `csharp-typed-engineer` is routing-and-planning-only until the planner preflight loop returns `PREFLIGHT: ALL CLEAR`. Before that signal, it must not run any state-changing implementation command and must not edit production or test files directly. All implementation and QA execution must occur via delegated handoffs.

Blocking rules in orchestrator mode:
- If the incoming request does not include the exact directive line, stop and request a corrected orchestrator handoff.
- If any delegation in the chain is skipped, treat the run as incomplete and do not report completion.
- Do not claim completion unless the final report includes all artifact paths from the feature review step.

## Documentation Expectations

Record in response/logs:
- estimated production file count,
- chosen path (`small`/`large`),
- rationale summary (1-3 bullets).

