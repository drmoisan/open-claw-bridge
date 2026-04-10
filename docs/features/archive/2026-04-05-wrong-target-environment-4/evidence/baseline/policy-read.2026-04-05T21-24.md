# Policy Read Evidence

- **Task:** P0-T2
- **Timestamp:** 2026-04-05T21-24
- **Feature:** wrong-target-environment (Issue #4)

## Files Read

1. `.github/copilot-instructions.md` — NOT FOUND (negative evidence; see `copilot-instructions-check.2026-04-05T21-24.md`)
2. `.github/instructions/general-code-change.instructions.md` — READ
3. `.github/instructions/general-unit-test.instructions.md` — READ
4. `.github/instructions/csharp-code-change.instructions.md` — READ
5. `.github/instructions/csharp-unit-test.instructions.md` — READ

## Applicable Policy Notes

### General Code Change

- **Pre-change:** Read existing change plans; document plan before executing.
- **Toolchain loop (mandatory, in order):** format → lint → type-check → test. Restart from format on any failure or auto-fix.
  - Formatting: `csharpier .`
  - Linting/Analyzers: MSBuild with `EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
  - Nullable/type-check: MSBuild with `Nullable=enable /p:TreatWarningsAsErrors=true`
  - Tests: `dotnet test` or `vstest.console.exe`
- **File size limit:** 500 lines per production/test file. No new files unless necessary.
- **Error handling:** Fail fast; no silent catch-all.
- **No new dependencies** unless explicitly approved.

### General Unit Test

- Tests must be independent, isolated, fast, deterministic.
- Coverage: repo-wide `>= 80%`; new modules `>= 90%`.
- Arrange–Act–Assert pattern required.
- No external dependencies, network, or filesystem temp files inside tests.

### C# Code Change

- **Formatter:** `csharpier .` (NOT `dotnet format` — it mis-handles legacy VSTO projects).
- **Analyzer build:** `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
- **Nullable build:** `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true`
- Keep nullable reference types enabled. Avoid broad suppressions.
- Avoid breaking public APIs.

### C# Unit Test

- **Framework:** MSTest only (`Microsoft.VisualStudio.TestTools.UnitTesting`). Do NOT use xUnit or NUnit.
- **Mocking:** Moq.
- **Assertions:** FluentAssertions preferred; MSTest Assert as fallback.
- **MSTest attributes:** `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`.
- **Test command:** `vstest.console.exe <assembly-paths> /EnableCodeCoverage`

## Required Commands (Toolchain)

1. `csharpier .`
2. `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
3. `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true`
4. `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal`
5. `vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage`
