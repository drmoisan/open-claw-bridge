---
name: harness-governance
description: The agent harness is now version-controlled; .claude/rules/* is the canonical policy source that AGENTS.md and .github/instructions/* must match.
metadata:
  type: project
---

As of Issue #66 / PR #68 (2026-06-08), the agent harness (`.claude/` and `.github/{agents,instructions,prompts,skills}`) is version-controlled. It was previously gitignored and had been copied unadapted from the "drm-copilot"/"TaskMaster" (No-COM Python/TypeScript/.NET) repository.

The single source of truth for policy is `.claude/rules/*`; `AGENTS.md` and `.github/instructions/*` are corrected to agree with it. Canonical coverage thresholds: line >= 85%, branch >= 75% (uniform across tiers). Real stack: .NET 10 + PowerShell + Outlook COM, `OpenClaw.MailBridge.sln`, MSTest/Moq/FluentAssertions, global `csharpier`, `dotnet build`/`dotnet test`, coverage via `mailbridge.runsettings`. No Python or TypeScript exists; those harness workers were removed. `.claude/hooks/**` is T4 harness tooling excluded from the application coverage surface.

**Why:** Agents reading the migrated policy were selecting the wrong tooling and a No-COM architecture that contradicts the product's core COM bridge.

**How to apply:** When editing harness policy, treat `.claude/rules/*` as authoritative and keep `AGENTS.md` + `.github/instructions/*` in sync. `AGENTS.md` is hand-maintained until `scripts/dev-tools/sync-agents-from-instructions.ps1` (a deferred follow-up) exists. Do not reintroduce xUnit/NSubstitute, No-COM, `TaskMaster.sln`, or `vstest.console.exe`/`msbuild` references.
