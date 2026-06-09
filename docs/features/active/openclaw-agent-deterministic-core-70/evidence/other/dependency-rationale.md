# Dependency Addition Rationale (Issue #70)

Timestamp: 2026-06-09T12-31

Per `.claude/rules/general-code-change.md` dependency policy, the following three test-only `PackageReference` additions to `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj` are documented with the reason each is required. All three are well-maintained, widely used packages and are added to the test project only (no production dependency is introduced).

1. `CsCheck` 4.7.0 — property-based testing. Required by the T1 property-test density gate in `.claude/rules/quality-tiers.md` and `.claude/rules/general-unit-test.md`, which mandates >= 1 property-based test per pure function. The deterministic agent pure functions (`Normalize`, `DependencyScorer.Score`, `TriageEngine.Triage`, `OwnerPriorityClassifier.Classify`, `RecurringMeetingClassifier.Classify`, `MovePolicy.CanMove`, `SlotProposer.ProposeTimes`) each receive a CsCheck property test. CsCheck is the C# property-based testing framework named in `.claude/rules/general-unit-test.md` ("Use `CsCheck` or `FsCheck` (C#) where applicable").

2. `Microsoft.Extensions.TimeProvider.Testing` 10.6.0 — deterministic `FakeTimeProvider`. Required by the determinism infrastructure in `.claude/rules/general-unit-test.md` and `.claude/rules/csharp.md`, which mandate `TimeProvider` injection and `FakeTimeProvider` for advancing simulated time in time-dependent tests (the slot proposer reads "now" only through an injected `TimeProvider`). Version 10.6.0 is compatible with `net10.0`.

3. `NetArchTest.Rules` 1.3.2 — namespace-scoped architecture-boundary assertion. Required to enforce the AC-10 / AC-U1 contract-parity invariant by namespace partition rather than assembly isolation, because the deterministic agent code is folded into the existing `OpenClaw.Core` project (no new project, no new `ProjectReference`, per `.claude/rules/architecture-boundaries.md` rule 6). `.claude/rules/architecture-boundaries.md` and `.claude/rules/csharp.md` both name `NetArchTest.Rules` as the automated boundary-assertion tool to add when assertions are introduced.

No production-project dependency was added. `OpenClaw.Core` retains its single `OpenClaw.HostAdapter.Contracts` ProjectReference and its existing `Microsoft.Data.Sqlite` package reference.
