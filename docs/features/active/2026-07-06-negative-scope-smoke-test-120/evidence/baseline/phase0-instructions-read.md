# Phase 0 — Policy Instructions Read (Issue #120)

Timestamp: 2026-07-06T23-11

Policy Order: Per `.claude/skills/policy-compliance-order`, policy files were read in the
required precedence order for the C#-only scope of this feature (`OpenClaw.Core`, tier T1).

Files read (in order):

1. `CLAUDE.md`-loaded standing instructions (auto-loaded path-scoped rules, always in effect).
2. `.claude/rules/general-code-change.md` — cross-language code change policy (design
   principles, mandatory seven-stage toolchain loop, 500-line file cap, error handling,
   naming, I/O boundaries).
3. `.claude/rules/general-unit-test.md` — cross-language unit test policy (five core
   properties, line >= 85% / branch >= 75% coverage, coverage-exclusion policy, scenario
   completeness, AAA structure, determinism infrastructure — `TimeProvider`/`FakeTimeProvider`,
   no temp files, banned wall-clock APIs).
4. `.claude/rules/csharp.md` — C# standards (CSharpier formatting, nullable analysis,
   naming, `internal` preference, file-scoped namespaces, CsCheck property tests, Stryker
   mutation, banned APIs `DateTime.Now`/`DateTime.UtcNow`/`Thread.Sleep`/`Task.Delay`,
   `TimeProvider` clock seam). Note: the repository has no `Directory.Build.props`,
   `.editorconfig`, `Directory.Packages.props`, `BannedSymbols.txt`, or third-party analyzer
   stack; `TreatWarningsAsErrors` is not set. The observed test harness is
   MSTest + Moq + FluentAssertions + CsCheck + `FakeTimeProvider` + `FakeHttpHandler`
   (a recorded pre-existing divergence from the xUnit/NSubstitute wording in this rule,
   per spec Constraints & Risks). New tests follow the observed harness.
5. `.claude/rules/quality-tiers.md` — module rigor tiers; `OpenClaw.Core` is T1 (uniform
   coverage line >= 85% / branch >= 75%; >= 1 property test per pure function; mutation
   >= 75% pre-merge/nightly; zero `dynamic`).
6. `.claude/rules/architecture-boundaries.md` — No-COM architecture boundary rules;
   enforced in .NET via `NetArchTest.Rules`. The pure-core dependency direction
   (`ScopeValidation` core must not depend on `CloudGraph`/HTTP/logging) is pinned by the
   new `ScopeValidationArchitectureBoundaryTests` (plan P5-T4).

Output Summary: All six policy sources were read prior to any code change. No policy
document was modified. The C#-only scope, T1 rigor obligations, the CSharpier-global-tool
invocation form, and the build-log warning-inspection requirement (warnings do not fail
the build because `TreatWarningsAsErrors` is unset) are confirmed and will govern
execution of Phases 1–6.
