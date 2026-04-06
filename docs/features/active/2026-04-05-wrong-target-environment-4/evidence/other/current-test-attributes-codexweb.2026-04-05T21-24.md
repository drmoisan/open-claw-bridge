# Current Test Attributes — CodexWebSetupScriptTests.cs

- **Task:** P1-T2
- **Timestamp:** 2026-04-05T21-24
- **Feature:** wrong-target-environment (Issue #4)
- **Source:** `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptTests.cs`

## Namespace Imports (NUnit-specific)

```csharp
using NUnit.Framework;
```

## NUnit Attributes Found

| Location | Attribute | Count |
|---|---|---|
| `CodexWebSetupScriptTests` class | (none — no `[TestFixture]`) | 0 |
| `Setup_script_should_restore_the_solution_with_the_available_dotnet_sdk` | `[Test]` | 1 |
| `Setup_script_should_restore_local_dotnet_tools_when_manifest_exists` | `[Test]` | 1 |
| `Setup_script_should_install_the_pinned_dotnet_sdk_when_dotnet_is_missing` | `[Test]` | 1 |
| `Setup_script_should_run_the_repo_bootstrap_hook_when_present` | `[Test]` | 1 |
| `Setup_script_should_report_git_metadata_and_status` | `[Test]` | 1 |

## NUnit API Usage

- `TestContext.CurrentContext.TestDirectory` used in `FindRepositoryRoot()` inside `CodexWebSetupScriptHarness` (private static method).

## Required MSTest Migration

- `using NUnit.Framework;` → `using Microsoft.VisualStudio.TestTools.UnitTesting;`
- Add `[TestClass]` to `CodexWebSetupScriptTests` class.
- `[Test]` (×5) → `[TestMethod]` (×5).
- `TestContext.CurrentContext.TestDirectory` → `AppContext.BaseDirectory` (MSTest has no global static `TestContext.CurrentContext`; `AppContext.BaseDirectory` is the test assembly directory equivalent).
