---
Timestamp: 2026-04-25T00-00
Policy Order:
  1. CLAUDE.md
  2. .claude/rules/general-code-change.md
  3. .claude/rules/general-unit-test.md
  4. .claude/rules/csharp.md
Files Read:
  - CLAUDE.md: Not present at repo root (no standing-instructions file found). Acknowledged as absent.
  - .claude/rules/general-code-change.md: Read and acknowledged.
  - .claude/rules/general-unit-test.md: Read and acknowledged.
  - .claude/rules/csharp.md: Read and acknowledged.
---

# Phase 0 Policy Read — Issue #45

## P0-T1 — CLAUDE.md

File `CLAUDE.md` was not found at the repository root. Acknowledged as absent; no standing overrides apply.

## P0-T2 — general-code-change.md

Key constraints acknowledged:
- Simplicity, reusability, separation of concerns.
- No file exceeds 500 lines.
- Full toolchain loop: format → lint → type-check → test; restart from step 1 on any failure.
- Fail fast with explicit errors; no silent ignoring.
- Isolate I/O into specific classes; core logic testable without I/O.

## P0-T3 — general-unit-test.md

Key constraints acknowledged:
- Repository-wide line coverage >= 80%; new code >= 90%.
- Tests: independent, isolated, fast, deterministic, readable.
- Arrange–Act–Assert structure.
- No external service dependencies; no temporary files in tests.
- Scenario completeness: positive, negative, edge cases, error handling.

## P0-T4 — csharp.md

Key constraints acknowledged:
- CSharpier for formatting: `dotnet tool run csharpier .`
- MSBuild with `EnableNETAnalyzers=true` and `EnforceCodeStyleInBuild=true` for linting.
  Note: csharp.md references `TaskMaster.sln` but the plan correctly identifies `OpenClaw.MailBridge.sln`.
- Nullable analysis: `Nullable=enable /p:TreatWarningsAsErrors=true`.
- MSTest + Moq + FluentAssertions for tests.
- Coverage: >= 80% repo-wide; >= 90% for new code.
- PascalCase for types/public members; camelCase for locals/private fields.
- XML doc comments on non-obvious APIs.
- `internal` visibility for non-public APIs.
