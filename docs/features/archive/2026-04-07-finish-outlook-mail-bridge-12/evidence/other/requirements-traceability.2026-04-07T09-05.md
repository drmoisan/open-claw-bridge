Timestamp: 2026-04-07T09-05
Requirements Sources:
- docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/spec.md
- docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/user-story.md
- docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/issue.md

## Framework Retarget
- Acceptance criteria: All production and test projects target `net8.0-windows`.
- Implementation files:
  - src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj
  - src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj
  - src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj
  - tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj

## COM Boundary
- Acceptance criteria: Outlook automation runs only on one dedicated STA thread in the primary interactive user session, follows the acquisition sequence, and performs disciplined COM cleanup on success and failure.
- Implementation files:
  - src/OpenClaw.MailBridge/OutlookStaExecutor.cs
  - src/OpenClaw.MailBridge/OutlookScanner.cs
  - src/OpenClaw.MailBridge/ComActiveObject.cs
  - src/OpenClaw.MailBridge/ScanWorker.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs

## Inbox Scanning
- Acceptance criteria: Default Inbox scanning uses `Restrict` on `ReceivedTime`, honors the overlap window, dedupes by stable item identity, normalizes `MailItem` and `MeetingItem`, and returns cache-backed message data.
- Implementation files:
  - src/OpenClaw.MailBridge/OutlookScanner.cs
  - src/OpenClaw.MailBridge/CacheRepository.cs
  - src/OpenClaw.MailBridge/BridgeStateStore.cs
  - src/OpenClaw.MailBridge/PipeRpcWorker.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs

## Calendar Scanning
- Acceptance criteria: Default Calendar scanning uses `Sort("[Start]")`, `IncludeRecurrences = true`, a bounded filter, a hard cap, no recurring-view `Count`, and returns cache-backed event data.
- Implementation files:
  - src/OpenClaw.MailBridge/OutlookScanner.cs
  - src/OpenClaw.MailBridge/CacheRepository.cs
  - src/OpenClaw.MailBridge/BridgeStateStore.cs
  - src/OpenClaw.MailBridge/PipeRpcWorker.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs

## Privacy Shaping
- Acceptance criteria: Safe mode suppresses protected fields; enhanced mode sanitizes and truncates previews; logs never contain raw body or attachment content.
- Implementation files:
  - src/OpenClaw.MailBridge/OutlookScanner.cs
  - src/OpenClaw.MailBridge/CacheRepository.cs
  - src/OpenClaw.MailBridge/PipeRpcWorker.cs
  - src/OpenClaw.MailBridge/Program.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs

## Repository
- Acceptance criteria: SQLite persistence and deterministic query behavior are complete for `messages`, `events`, and `scan_state`, including stale-cache handling.
- Implementation files:
  - src/OpenClaw.MailBridge/CacheRepository.cs
  - src/OpenClaw.MailBridge/BridgeStateStore.cs
  - src/OpenClaw.MailBridge/PipeRpcWorker.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs

## Pipe ACL
- Acceptance criteria: The named-pipe server validates requests, enforces explicit grants and denies, and fails hard when required identities such as `openclaw-svc` cannot be resolved.
- Implementation files:
  - src/OpenClaw.MailBridge/PipeRpcWorker.cs
  - src/OpenClaw.MailBridge/Program.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs
  - scripts/test-mailbridge.ps1

## Client CLI
- Acceptance criteria: The client resolves the configured pipe name, supports `--pipe-name`, preserves JSON-only stdout, writes diagnostics to stderr, and maps bridge errors deterministically to exit codes.
- Implementation files:
  - src/OpenClaw.MailBridge.Client/Program.cs
  - src/OpenClaw.MailBridge.Contracts/
  - tests/OpenClaw.MailBridge.Tests/MailBridgeClientTests.cs
  - scripts/test-mailbridge.ps1

## Scripts And Runbook
- Acceptance criteria: Install, register, uninstall, acceptance-test scripts, README, and runbook match the finished bridge behavior and the scripted/operator evidence split.
- Implementation files:
  - scripts/install-mailbridge.ps1
  - scripts/register-mailbridge-task.ps1
  - scripts/uninstall-mailbridge.ps1
  - scripts/test-mailbridge.ps1
  - docs/mailbridge-runbook.md
  - README.md
  - tests/scripts/install-mailbridge.Tests.ps1
  - tests/scripts/register-mailbridge-task.Tests.ps1
  - tests/scripts/test-mailbridge.Tests.ps1
  - tests/scripts/uninstall-mailbridge.Tests.ps1

## Test Expansion
- Acceptance criteria: Deterministic MSTest and Pester coverage proves actual runtime, repository, client, and script behavior rather than placeholders.
- Implementation files:
  - tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs
  - tests/OpenClaw.MailBridge.Tests/MailBridgeClientTests.cs
  - tests/scripts/register-mailbridge-task.Tests.ps1
  - tests/scripts/install-mailbridge.Tests.ps1
  - tests/scripts/test-mailbridge.Tests.ps1
  - tests/scripts/uninstall-mailbridge.Tests.ps1

Issue-only topology constraints:
- Keep all work inside:
  - src/OpenClaw.MailBridge/
  - src/OpenClaw.MailBridge.Client/
  - src/OpenClaw.MailBridge.Contracts/
  - tests/OpenClaw.MailBridge.Tests/
  - scripts/install-mailbridge.ps1
  - scripts/register-mailbridge-task.ps1
  - scripts/uninstall-mailbridge.ps1
  - scripts/test-mailbridge.ps1
  - docs/mailbridge-runbook.md
- Prohibited replacements and scope expansion:
  - No HTTP, WebSocket, Microsoft Graph, EWS, or Windows service replacement.
  - No write/send/reply/update Outlook capabilities.
  - No full body or attachment payload exposure.
