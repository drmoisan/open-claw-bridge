---
name: harness-canonical-policy
description: Operator decision (2026-06-08, Issue #66) that .claude/rules/* is the single source of truth for the agent harness; .github/instructions/* and AGENTS.md are corrected to match it
metadata:
  type: project
---

For the agent harness (`.claude/`, `.github/agents`, `.github/instructions`, `AGENTS.md`), `.claude/rules/*` is the canonical single source of truth. `.github/instructions/*` and `AGENTS.md` are corrected to agree with it.

Confirmed canonical parameters (Issue #66, operator-confirmed 2026-06-08):
- Coverage thresholds: line >= 85%, branch >= 75% (uniform). The old `.github/instructions` 80%/90% model is wrong and being removed.
- Test stack: MSTest + Moq + FluentAssertions (not xUnit/NSubstitute).
- Solution: `OpenClaw.MailBridge.sln`; build/test via `dotnet build`/`dotnet test` (not `msbuild TaskMaster.sln`/`vstest.console.exe`).
- Formatting: global `csharpier` only — no `.config/dotnet-tools.json`, no `dotnet tool run csharpier`, no `Directory.Build.props` analyzer-config claims.
- Coverage runsettings: `mailbridge.runsettings` at repo root.

**Why:** The `.claude/`/`.github/` harness was copied wholesale from the "drm-copilot"/"TaskMaster" repo (No-COM Python/TS/.NET) without per-file adaptation, leaving residual markers and references to absent files. `.claude/rules/csharp.md` and `architecture-boundaries.md` were already corrected first, making `.claude/rules/*` the most-correct system.

**How to apply:** When editing any harness document, treat `.claude/rules/*` as authoritative; reconcile `.github/instructions/*` and `AGENTS.md` to it rather than the reverse. `AGENTS.md` is generated from `.github/instructions/*` but the generator (`scripts/dev-tools/sync-agents-from-instructions.ps1`) is absent, so hand-edit both in the same change until that script exists. See [[harness-migration-deferred-followups]].
