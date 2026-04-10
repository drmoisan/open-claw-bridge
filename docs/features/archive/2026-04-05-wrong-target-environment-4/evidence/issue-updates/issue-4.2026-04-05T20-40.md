Issue: #4
Timestamp: 2026-04-05T21:10:27.6044505-04:00

Summary:
- Confirmed all MailBridge projects now target `net10.0-windows`.
- Verified test execution and build outputs now reference `net10.0-windows`, not `net8.0-windows`.
- Applied one additional test-only fix so the setup-script harness ignores inherited `DOTNET_*` variables and consistently uses its fake `dotnet` shim during verification.

Retargeted project files:
- `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`
- `src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj`
- `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj`
- `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`

Verification commands:
- `dotnet test tests\OpenClaw.MailBridge.Tests\OpenClaw.MailBridge.Tests.csproj -v minimal`
- `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true`
- `dotnet msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug /p:Platform='Any CPU' /p:Nullable=enable /p:TreatWarningsAsErrors=true`
- `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe tests\OpenClaw.MailBridge.Tests\bin\Debug\net10.0-windows\OpenClaw.MailBridge.Tests.dll /EnableCodeCoverage`

Observed outcomes:
- `dotnet test` passed 8 of 8 tests on `net10.0-windows`.
- Analyzer-enabled and nullable-as-errors builds both passed.
- `vstest.console.exe` passed 8 of 8 tests and produced coverage output for the `net10.0-windows` test assembly.

Remaining caveats:
- No remaining runtime-targeting issue was observed in the MailBridge projects after retargeting.
- Separate from this repo, the parent-level `C:\Users\DanMoisan\repos\dotnet-tools.json` manifest still points at an older `csharpier` command shape, so audit formatting used an isolated temporary tool installation and removed it afterward.

