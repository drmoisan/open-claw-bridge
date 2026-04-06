# Policy Audit

- Timestamp: 2026-04-05T21-30
- Feature: `docs/features/active/2026-04-05-wrong-target-environment-4`
- Review mode: `minor-audit`
- Base branch: `development`
- Head branch: `wrong-target-environment-4`
- Audit type: Post-remediation re-audit (supersedes `policy-audit.2026-04-05T21-24.md`)
- Provenance: canonical PR context artifacts at `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt`; direct file inspection of test project and test source files.
- Template source: Minimal fallback artifact (template not found at `docs/features/templates/policy_audit/policy-audit.yyyy-MM-ddTHH-mm.md`).
- Feature folder selection rule: single active feature folder matching branch name suffix `wrong-target-environment-4`.

## Context

This is a re-audit following successful NUnit → MSTest remediation executed under
`remediation-plan.2026-04-05T21-24.md`. The prior audit (`2026-04-05T21-24`) returned a
FAIL on the C# Unit Test Policy because the test project used NUnit. All remediation plan
tasks are `[x]` complete.

## Files Under Test (changed relative to `development`)

| File | Change type |
|---|---|
| `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` | Modified — NUnit → MSTest packages |
| `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptTests.cs` | Modified — NUnit → MSTest attributes |
| `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs` | Modified — NUnit → MSTest attributes |
| `docs/features/active/2026-04-05-wrong-target-environment-4/issue.md` | Modified — AC section added |
| `docs/features/active/2026-04-05-wrong-target-environment-4/evidence/*` | Untracked — new audit/QA evidence files |
| `docs/features/active/2026-04-05-wrong-target-environment-4/*.md` | Untracked — audit/remediation artifacts |

Source project files (`src/`) are unchanged relative to `development`.

## Policy Results

### ✅ PASS — General Code Change Policy

Evidence:
- Active plan completed: `docs/features/active/2026-04-05-wrong-target-environment-4/plan.2026-04-05T20-40.md` (all tasks `[x]`)
- Remediation plan completed: `docs/features/active/2026-04-05-wrong-target-environment-4/remediation-plan.2026-04-05T21-24.md` (all tasks `[x]`)
- Full Phase 0 baseline (policy read, git state, dotnet version/SDKs/runtimes) captured under `evidence/baseline/` with `2026-04-05T21-24` timestamps.
- QA gates captured under `evidence/qa-gates/` and `evidence/regression-testing/`.

Notes:
- Changes are minimal and targeted: only test packaging/attributes and issue.md AC section were touched; `src/` production code is unchanged.
- No 500-line file limit violations observed.

### ✅ PASS — General Unit Test Policy

Evidence:
- `dotnet test` passed: `evidence/regression-testing/dotnet-test.2026-04-05T21-24.md` — EXIT_CODE: 0, 8/8 tests passed on `net10.0-windows`.
- `vstest.console.exe` passed: `evidence/qa-gates/vstest.2026-04-05T21-24.md` — EXIT_CODE: 0, 8/8 tests passed on `net10.0-windows`.
- No tests removed or skipped; scenarios identical pre/post migration.
- Tests are deterministic, isolated, and do not use temporary files; the `DOTNET_*` harness isolation behavior is preserved.

### ✅ PASS — C# Code Change Policy

Evidence:
- All 4 projects target `net10.0-windows` (direct inspection of `.csproj` files):
  - `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj:4`
  - `src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj:3`
  - `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj:4`
  - `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj:3`
- Formatter (csharpier): `evidence/qa-gates/format.2026-04-05T21-24.md` — EXIT_CODE: 0, 10 files checked, no changes.
- Analyzer build: `evidence/qa-gates/analyzer-build.2026-04-05T21-24.md` — EXIT_CODE: 0, all 4 projects succeeded with `EnableNETAnalyzers=true` and `EnforceCodeStyleInBuild=true`.
- Nullable build: `evidence/qa-gates/nullable-build.2026-04-05T21-24.md` — EXIT_CODE: 0, all 4 projects succeeded with `Nullable=enable` and `TreatWarningsAsErrors=true`.

### ✅ PASS — C# Unit Test Policy *(previously FAIL — now remediated)*

Evidence:
- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj` — direct inspection confirms:
  - `MSTest.TestAdapter Version="3.6.4"` present
  - `MSTest.TestFramework Version="3.6.4"` present
  - No `NUnit` or `NUnit3TestAdapter` references
- `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs` — direct inspection confirms:
  - `using Microsoft.VisualStudio.TestTools.UnitTesting;` (no NUnit import)
  - `[TestClass]` on class
  - `[TestMethod]` on all 3 test methods
- `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptTests.cs` — direct inspection confirms:
  - `using Microsoft.VisualStudio.TestTools.UnitTesting;` (no NUnit import)
  - `[TestClass]` on class
  - `[TestMethod]` on all 5 test methods
  - `AppContext.BaseDirectory` used in place of `TestContext.CurrentContext.TestDirectory` (MSTest-compatible)
- Assertion library: `FluentAssertions 6.12.0` retained per policy.
- End-state diff: `evidence/other/end-state.2026-04-05T21-24.md` confirms migration scope.

Prior FAIL finding from `policy-audit.2026-04-05T21-24.md` is fully resolved.

## Appendix B — Commands Run

All commands were run by the remediation executor during the `2026-04-05T21-24` remediation pass.
This re-audit relies on those evidence artifacts as its toolchain record, since no new code
changes occurred between T21-24 and this review.

| Step | Command | Evidence file | Exit code |
|---|---|---|---|
| Format | `csharpier format .` then `csharpier check .` | `evidence/qa-gates/format.2026-04-05T21-24.md` | 0 |
| Analyzer build | `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` | `evidence/qa-gates/analyzer-build.2026-04-05T21-24.md` | 0 |
| Nullable build | `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` | `evidence/qa-gates/nullable-build.2026-04-05T21-24.md` | 0 |
| dotnet test | `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal --no-build` | `evidence/regression-testing/dotnet-test.2026-04-05T21-24.md` | 0 |
| vstest | `vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage` | `evidence/qa-gates/vstest.2026-04-05T21-24.md` | 0 |

## Overall

- Status: `PASS`
- Recommendation: **Ready for merge** — all four policy categories pass; the prior C# Unit Test Policy FAIL is fully remediated; all QA gates clean.
