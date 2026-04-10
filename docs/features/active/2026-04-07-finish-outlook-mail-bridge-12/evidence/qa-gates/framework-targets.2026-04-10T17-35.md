# Framework Targets — QA Gate Evidence

Timestamp: 2026-04-10T17-35
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass (framework targets validation — project TFMs + installed runtimeconfig verification)
EXIT_CODE: 0
Output Summary: ProjectTargets: PASS, InstalledRuntimeConfigs: PASS, BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0, ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0.

## Validation Results

- ProjectTargets: PASS
  - `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`: net10.0-windows confirmed
  - `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj`: net10.0-windows confirmed
  - `src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj`: net10.0-windows confirmed
  - `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`: net10.0-windows confirmed
- InstalledRuntimeConfigs: PASS
  - `C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.runtimeconfig.json`: tfm=net10.0
  - `C:\Program Files\OpenClaw\MailBridge\OpenClaw.MailBridge.Client.runtimeconfig.json`: tfm=net10.0
- BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0
- ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0
