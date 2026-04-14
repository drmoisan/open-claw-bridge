# Policy Audit

- Timestamp: 2026-04-06T20-25
- Feature: `docs/features/active/2026-04-05-refactor-and-test-9`
- Review mode: `full-feature`
- Base branch: `development` (resolved by merge-base because no explicit PR base was provided)
- Head branch: `refactor/refactor-and-test-9`
- Provenance: refreshed fallback `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`, direct code inspection, and fresh Windows validation commands from this review run.
- Template source: minimal fallback artifact because `docs/features/templates/policy_audit/policy-audit.yyyy-MM-ddTHH-mm.md` is not present in this repository.
- Feature folder selection rule: explicit user-provided folder `docs/features/active/2026-04-05-refactor-and-test-9`.

## Context

This review covers the feature branch relative to `development`. The branch does deliver the core runtime split out of `Program.cs`, but the active feature docs are not in a mergeable state and the verification evidence does not satisfy all scoped acceptance and coverage requirements.

## Files Under Review

### In-scope runtime and test files

- `src/OpenClaw.MailBridge/Program.cs`
- `src/OpenClaw.MailBridge/BridgeApplication.cs`
- `src/OpenClaw.MailBridge/BridgeStateStore.cs`
- `src/OpenClaw.MailBridge/CacheRepository.cs`
- `src/OpenClaw.MailBridge/ComActiveObject.cs`
- `src/OpenClaw.MailBridge/OutlookScanner.cs`
- `src/OpenClaw.MailBridge/OutlookStaExecutor.cs`
- `src/OpenClaw.MailBridge/PipeRpcWorker.cs`
- `src/OpenClaw.MailBridge/ScanWorker.cs`
- `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`
- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`
- `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs`
- `tests/OpenClaw.MailBridge.Tests/BridgeContractsCoverageTests.cs`

### Scope contamination present on the branch

- `.codex/codex-web-setup.sh`
- `AGENTS.md`
- prior draft review artifacts under `docs/features/active/2026-04-05-refactor-and-test-9/`

## Policy Results

### ❌ FAIL — General Code Change Policy

Evidence:
- Authoritative feature docs are not mergeable because they contain unresolved merge conflict markers:
  - `docs/features/active/2026-04-05-refactor-and-test-9/issue.md:4,50,69`
  - `docs/features/active/2026-04-05-refactor-and-test-9/spec.md:1,80,95`
  - `docs/features/active/2026-04-05-refactor-and-test-9/plan.2026-04-06T14-25.md:1,49,67`
- The active plan is internally inconsistent with the evidence on disk. `plan.2026-04-06T14-25.md` marks `[P3-T2] Generate coverage report and verify per-file coverage target` as complete, but the branch evidence explicitly says some refactored runtime files are below 80% (`evidence/qa-gates/coverage.2026-04-06T14-55.md:4`).
- The branch diff contains unrelated files outside the scoped runtime refactor (`.codex/codex-web-setup.sh`, `AGENTS.md`), which is scope creep relative to the feature docs.

Why this fails policy:
- The repo requires supporting documents and change plans to stay synchronized with actual delivery.
- Unresolved merge markers make the requirements and plan non-authoritative.
- The branch includes changes not described by the active feature scope.

### ❌ FAIL — General Unit Test Policy

Evidence:
- New unit tests in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs` create and delete temporary files/directories, which the repo policy explicitly forbids for unit tests:
  - `Path.GetTempPath()` at lines `29`, `50`, `65`, `83`
  - `Directory.CreateDirectory(...)` at lines `52`, `67`, `85`
  - `File.WriteAllTextAsync(...)` at lines `53`, `68`, `86`
  - `Directory.Delete(...)` at lines `41`, `58`, `77`, `90`
- Fresh Windows coverage collection shows the overall line rate is `89.3%`, but targeted runtime files are still below the stated feature threshold and below the repo’s new-module expectation in multiple cases:
  - `BridgeApplication.cs`: `71.9%`
  - `ComActiveObject.cs`: `53.8%`
  - `OutlookScanner.cs`: `81.0%`
  - `PipeRpcWorker.cs`: `93.5%`
  - `ScanWorker.cs`: `88.9%`
- `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build` passed with `33` succeeded and `1` skipped, so the suite is green enough for smoke validation, but not all policy expectations are satisfied.

Why this fails policy:
- The general unit-test policy bans temporary files unless an explicit exception exists; none exists here.
- New production classes introduced by the refactor do not meet the repo’s coverage expectations.

### ⚠️ PARTIAL — C# Code Change Policy

Evidence:
- `csharpier check .` passed: `Checked 20 files in 318ms.`
- Analyzer-enabled build passed via fallback command:
  - Required command by policy: `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
  - Actual review-run result: `msbuild` was unavailable on PATH, so `dotnet msbuild ...` was used and succeeded.
- Nullable/type-safety build passed via fallback command:
  - Required command by policy: `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true`
  - Actual review-run result: `msbuild` was unavailable on PATH, so `dotnet msbuild ...` was used and succeeded.
- Structural goal achieved: `src/OpenClaw.MailBridge/Program.cs` now contains only the entry point and `BridgeApplication` owns the host wiring.

Why this is only partial:
- The code itself is analyzer-clean in the fallback run, but this review could not execute the exact approved `msbuild` command because the host environment lacked `msbuild` on PATH.
- The branch still fails broader repo policy because the scoped docs and tests are not compliant.

### ⚠️ PARTIAL — C# Unit Test Policy

Evidence:
- Framework and assertion selection are compliant:
  - `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` uses `MSTest.TestAdapter`, `MSTest.TestFramework`, and `FluentAssertions`.
- Fresh review-run testing used:
  - `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build` -> exit `0`
  - `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --collect:"XPlat Code Coverage"` -> exit `0`
- The repo-preferred `vstest.console.exe <assembly> /EnableCodeCoverage` command was not executed during this review run.

Why this is only partial:
- The framework selection is correct, but the repo’s preferred final C# test command was not captured in this audit run.
- Coverage expectations for the touched runtime area remain unmet.

## Coverage Metrics

The review prompt requires numeric baseline, post-change, and new-code coverage values for a PASS-style policy outcome. This branch does not have a PASS-style outcome because coverage evidence is incomplete and partially failing.

| Metric | Status | Evidence |
|---|---|---|
| Baseline coverage | Missing | No baseline coverage artifact exists for the pre-refactor state in this feature folder. |
| Post-change overall line coverage | `89.3%` | Fresh Windows `dotnet test --collect:"XPlat Code Coverage"` run in this review session. |
| Post-change targeted file coverage | Mixed / failing | `BridgeApplication.cs` = `71.9%`, `ComActiveObject.cs` = `53.8%`, others listed above. |
| New/changed-code coverage | Missing policy-grade evidence | No artifact records a numeric changed-line or new-code coverage result suitable for PASS gating. |

Because baseline and new/changed-code coverage values are missing, and because not all targeted files meet the feature’s `80%+` requirement, this audit cannot return PASS.

## Appendix B — Commands Run

| Step | Command | Result |
|---|---|---|
| Format | `csharpier check .` | Exit `0` |
| Analyzer build (required command) | `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` | Could not run: `msbuild` not on PATH |
| Analyzer build (fallback used) | `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` | Exit `0` |
| Nullable build (required command) | `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true` | Could not run: `msbuild` not on PATH |
| Nullable build (fallback used) | `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true` | Exit `0` |
| Solution tests | `dotnet test OpenClaw.MailBridge.sln -c Debug --no-build` | Exit `0` (`33` passed, `1` skipped) |
| Coverage run | `dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --collect:"XPlat Code Coverage" --results-directory TestResults/review-coverage` | Exit `0`; overall line coverage `89.3%` |
| Repo-preferred coverage test command | `vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage` | Not executed in this review run |

## Overall

- Status: `FAIL`
- Recommendation: **Needs revision before PR**

The runtime refactor itself is largely in place, but the branch is not merge-ready. The authoritative feature docs are conflicted, the unit tests violate repo policy by using temporary files, the coverage target is not met for all targeted runtime files, and the branch carries unrelated changes outside the scoped feature.