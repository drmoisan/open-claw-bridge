# Issue #4 Remediation Outcome

- **Task:** P4-T2
- **Timestamp:** 2026-04-05T21-24
- **Feature:** wrong-target-environment (Issue #4)
- **Branch:** wrong-target-environment-4

## Remediation Summary

### What Was Done

The `OpenClaw.MailBridge.Tests` project was migrated from NUnit to MSTest to comply with the C# unit test policy (`csharp-unit-test.instructions.md`).

**Package changes (`OpenClaw.MailBridge.Tests.csproj`):**
- Removed: `NUnit 3.14.0`, `NUnit3TestAdapter 4.5.0`
- Added: `MSTest.TestAdapter 3.6.4`, `MSTest.TestFramework 3.6.4`
- Retained: `FluentAssertions 6.12.0`, `Microsoft.NET.Test.Sdk 17.10.0`
- Target framework `net10.0-windows` — unchanged

**`CodexWebSetupScriptTests.cs`:**
- `using NUnit.Framework;` → `using Microsoft.VisualStudio.TestTools.UnitTesting;`
- Added `[TestClass]` to the test class
- 5× `[Test]` → `[TestMethod]`
- `TestContext.CurrentContext.TestDirectory` → `AppContext.BaseDirectory` (MSTest equivalent for test assembly base directory)
- All `DOTNET_*` environment variable clearing in the process harness (`RunAsync`) preserved exactly

**`MailBridgeTests.cs`:**
- `using NUnit.Framework;` → `using Microsoft.VisualStudio.TestTools.UnitTesting;`
- Added `[TestClass]` to the test class
- 3× `[Test]` → `[TestMethod]`
- All FluentAssertions-based assertions preserved exactly

### Verification Results

| Gate | Command | EXIT_CODE | Result |
|---|---|---|---|
| Format | `csharpier check .` | 0 | PASS |
| Analyzer Build | `dotnet msbuild ... /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` | 0 | PASS |
| Nullable Build | `dotnet msbuild ... /p:Nullable=enable /p:TreatWarningsAsErrors=true` | 0 | PASS |
| dotnet test | `dotnet test tests\...\OpenClaw.MailBridge.Tests.csproj -v minimal` | 0 | 8/8 PASS |
| vstest.console.exe | `vstest.console.exe ...net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage` | 0 | 8/8 PASS |

### Residual Caveats

- None. All 8 tests pass on `net10.0-windows`. No tests were removed or weakened.
- The `DOTNET_*` harness isolation pattern in `CodexWebSetupScriptHarness.RunAsync()` is fully preserved.
- `AppContext.BaseDirectory` is functionally equivalent to the former `TestContext.CurrentContext.TestDirectory` for the purposes of `FindRepositoryRoot()` — both return the test assembly's output directory, from which the solution file can be found by traversing upward.

## Acceptance Criteria Status

All AC items verified PASS. See `issue.md` for checkbox updates.
