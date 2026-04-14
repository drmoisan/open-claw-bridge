# Remediation Inputs

- Timestamp: 2026-04-05T21-24
- Trigger: feature review found a `FAIL` in the C# unit-test policy audit.

## Enumerated Fixes

1. Migrate the touched MailBridge test project from NUnit to MSTest.
2. Preserve the existing test scenarios and assertions while changing only the test framework integration needed for policy compliance.

## Exact Files In Scope

- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`
- `tests/OpenClaw.MailBridge.Tests/CodexWebSetupScriptTests.cs`
- `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs`

## Expected Behavior

- The test project should use MSTest packages and MSTest attributes, not NUnit.
- Existing scenarios should continue to pass on `net10.0-windows`.
- The harness behavior that now clears inherited `DOTNET_*` variables must remain intact.

## Verification Commands

- `csharpier .`
- `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
- `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true`
- `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal`
- `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage`

## Do-Not-Do List

- Do not change the four project target frameworks away from `net10.0-windows`.
- Do not widen scope into unrelated production-code refactors.
- Do not remove or weaken the setup-script harness coverage added in `CodexWebSetupScriptTests.cs`.
- Do not switch to another non-MSTest framework.

