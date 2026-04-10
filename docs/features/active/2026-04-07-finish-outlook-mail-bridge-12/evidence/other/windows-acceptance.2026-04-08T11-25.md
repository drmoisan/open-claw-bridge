Timestamp: 2026-04-08T11-25
Command: pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-mailbridge.ps1
EXIT_CODE: 1
Output Summary: Published/runtime framework validation passed (`PublishedBridgeTargetFramework: net10.0-windows`, `PublishedClientTargetFramework: net10.0-windows`, `BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0`, `ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0`), but suite A failed before readiness with `No JSON output returned for arguments: status`. Supporting checks confirmed the `OpenClaw MailBridge` scheduled task exists, installed runtimeconfig files exist, and `Get-ScheduledTaskInfo` reported `LastTaskResult: 267009` after a manual trigger attempt.
PublishedBridgeTargetFramework: net10.0-windows
PublishedClientTargetFramework: net10.0-windows
BridgeRuntimeFramework: Microsoft.NETCore.App 10.0.0
ClientRuntimeFramework: Microsoft.NETCore.App 10.0.0
AutomatedSuitesPassed: FAILED_BEFORE_A
Failure: No JSON output returned for arguments: status
SupportingEvidence:
- ScheduledTask: PRESENT
- TaskStateAtCheck: Ready
- SettingsExists: True
- BridgeRuntimeConfigExists: True
- ClientRuntimeConfigExists: True
- ScheduledTaskLastTaskResult: 267009
