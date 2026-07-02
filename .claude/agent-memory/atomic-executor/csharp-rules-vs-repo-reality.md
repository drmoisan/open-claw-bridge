---
name: csharp-rules-vs-repo-reality
description: open-claw-bridge repo lacks the analyzer infra csharp.md describes (no Directory.Build.props, .editorconfig, BannedSymbols.txt); test stack is MSTest+Moq+FluentAssertions, not xUnit+NSubstitute
metadata:
  type: project
---

`.claude/rules/csharp.md` describes aspirational tooling that does not exist in the open-claw-bridge repo: there is no `Directory.Build.props`, no `Directory.Packages.props`, no root `.editorconfig`, and no `BannedSymbols.txt`, so `dotnet build` runs no analyzer stack and `TreatWarningsAsErrors` is off. The actual test stack is MSTest + FluentAssertions + Moq + CsCheck + FakeTimeProvider + NetArchTest (see `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj`), not the xUnit + NSubstitute the rule prescribes. Package versions are pinned inline per csproj.

**Why:** Verified during issue #113 execution (2026-07-02); the spec/plan for that feature explicitly recorded "new tests follow the repository reality" as established precedent from prior features.

**How to apply:** When executing C# plans here, follow the repo reality (MSTest/Moq) and do not add analyzer packages or config files unless a plan task says so. Banned-API discipline (`TimeProvider` instead of `DateTime.UtcNow`, no `Task.Delay`/`Thread.Sleep`) is still policy-enforced by review even though no analyzer catches it. See [[coverlet-async-body-exclusion]] for the coverage-instrumentation side.
