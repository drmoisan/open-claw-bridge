# Phase 0 — Policy Instructions Read

Timestamp: 2026-07-02T15-04

Policy Order:
1. `.claude/rules/general-code-change.md`
2. `.claude/rules/general-unit-test.md`
3. `.claude/rules/csharp.md`
4. `.claude/rules/quality-tiers.md`
5. `.claude/rules/architecture-boundaries.md`
6. `.claude/rules/tonality.md`

Files read:
- `.claude/rules/general-code-change.md` — cross-language code change policy (design principles, toolchain loop, 500-line cap, error handling, naming, dependencies, I/O boundaries)
- `.claude/rules/general-unit-test.md` — unit test policy (five core properties, coverage >= 85% line / >= 75% branch, no temp files, determinism infrastructure, test file location under `tests/`)
- `.claude/rules/csharp.md` — C# toolchain (CSharpier, analyzers via `dotnet build`, coverage collection), banned APIs (`DateTime.Now`/`UtcNow`, `Task.Delay`, `Thread.Sleep`, `Random.Shared`), `TimeProvider`/`FakeTimeProvider` clock seam, DI seam preferences
- `.claude/rules/quality-tiers.md` — T1–T4 tier system; `OpenClaw.Core` is T1 (uniform coverage thresholds; property tests >= 1 per pure function on T1/T2)
- `.claude/rules/architecture-boundaries.md` — No-COM architecture assertions; boundary tests are a uniform blocking gate
- `.claude/rules/tonality.md` — professional tone; no humor, hyperbole, or decorative metaphor

Note: `.claude/rules/csharp.md` names xUnit/NSubstitute as defaults; the approved plan and the existing `tests/OpenClaw.Core.Tests` suite use MSTest + FluentAssertions + Moq + CsCheck. Per the plan conventions (source of truth, preflight ALL CLEAR), the existing repo test stack is followed.
