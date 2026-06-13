# Baseline — OutlookScanner autostart-failure robustness fix

Timestamp: 2026-06-06T14-40
Policy Order: CLAUDE.md, general-code-change.md, general-unit-test.md, csharp.md, architecture-boundaries.md, benchmark-baselines.md, ci-workflows.md
Files read: src/OpenClaw.MailBridge/OutlookScanner.cs, src/OpenClaw.MailBridge/ComActiveObject.cs, src/OpenClaw.MailBridge/BridgeStateStore.cs, tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs, tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs

## Format
Command: csharpier check .
EXIT_CODE: 0
Output Summary: Checked 94 files; all formatted.

## Build (analyzers + nullable as errors)
Command: dotnet build src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj
EXIT_CODE: 0
Output Summary: Build succeeded, 0 Warning(s), 0 Error(s).

## Test + coverage
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary: Failed: 0, Passed: 173, Skipped: 3, Total: 176.
OutlookScanner coverage: line-rate 0.9277 (92.77%), branch-rate 0.8571 (85.71%).

## Notes
- Repo uses csharpier as a dotnet GLOBAL tool (command `csharpier`, v1.2.6). There is no local
  `.config/dotnet-tools.json` manifest, so `dotnet tool restore` / `dotnet csharpier` fail; the
  global `csharpier` binary is the working invocation.
- Existing OutlookScanner test file uses MSTest (`[TestMethod]`) + a hand-written `FakeComActiveObject`
  test double (subclass of the `virtual` ComActiveObject), not xUnit/NSubstitute. New tests match the
  existing framework and double in that file per general-unit-test policy (match existing framework).
