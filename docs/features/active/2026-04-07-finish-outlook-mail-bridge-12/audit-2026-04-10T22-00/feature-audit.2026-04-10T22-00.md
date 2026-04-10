# Feature Audit

- Timestamp: 2026-04-10T22-00
- Feature: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`
- Issue: #12
- Review mode: `full-feature`
- Base branch: `development`
- Head branch: `feature/finish-outlook-mail-bridge-12`
- Head commit: `d344f1810acdd2e9f583e4e4f23110aff271312d`

## Scope and Baseline

- Base branch: `development` (explicitly provided in handoff)
- Evidence sources:
  - PR context summary: `artifacts/pr_context.summary.txt` (collected 2026-04-10 21:54 UTC)
  - PR context appendix: `artifacts/pr_context.appendix.txt` (collected 2026-04-10 21:54 UTC)
- Feature folder: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12`
- Work mode: `full-feature` (from `issue.md` line: `- Work Mode: full-feature`)
- AC source files: `spec.md` and `user-story.md` (per `full-feature` mode rules)
- Merge base: `886afaa7afdd2e60e7e9e25464f81cd6293bbf7e`
- Range: `886afaa7afdd2e60e7e9e25464f81cd6293bbf7e..d344f1810acdd2e9f583e4e4f23110aff271312d`

## Acceptance Criteria Inventory

Extracted from `spec.md` (Definition of Done) and `user-story.md` (Acceptance Criteria). The `user-story.md` acceptance criteria are the primary authoritative checklist (10 items). The `spec.md` Definition of Done items (7 items) overlap and are reconciled below.

### Primary AC Source: `user-story.md` — Acceptance Criteria (10 items)

1. All production and test projects target `net10.0-windows`.
2. Outlook automation runs only on one dedicated STA thread in the primary interactive user session, follows the required acquisition sequence, and performs disciplined COM cleanup on both success and failure paths.
3. Default Inbox scanning uses `Restrict` on `ReceivedTime`, a 30-second poll interval, a 5-minute overlap window, dedupes by `EntryID`, normalizes both `MailItem` and `MeetingItem`, and returns real cached message data from the message RPC methods.
4. Default Calendar scanning uses `Items.Sort`, `IncludeRecurrences = true`, a bounded start/end filter, avoids `Count` on recurring views, enforces a hard cap, and returns real cached event data from the calendar RPC methods.
5. Safe mode and enhanced mode are both implemented correctly, privacy redaction and preview sanitization/truncation match the fixed spec, and neither attachment content nor message/event body content is logged.
6. SQLite persistence and query behavior are complete for `messages`, `events`, and `scan_state`, including deterministic queries and the required stale-cache behavior.
7. The named-pipe server exposes only the allowed methods, validates requests deterministically, returns the specified error codes, applies explicit `PipeSecurity` grants and denies, and fails hard when SID or ACL resolution (including `openclaw-svc`) cannot be completed.
8. `OpenClaw.MailBridge.Client.exe` supports only the specified commands, writes JSON only to stdout, writes diagnostics only to stderr, does not rely on a hard-coded pipe name, and uses deterministic exit-code mapping.
9. `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1`, and `docs/mailbridge-runbook.md` implement and document the required preflight checks, on-logon interactive task registration, operational guidance, and automated acceptance suite A-F.
10. Tests in `tests/OpenClaw.MailBridge.Tests` prove actual behavior rather than stubs, covering DTO/ID helpers, strict config validation, redaction/sanitization, repository persistence/query behavior, RPC validation/error mapping, safe vs enhanced response shapes, stale-cache handling, COM boundary orchestration, and practical script assertions.

### Secondary AC Source: `spec.md` — Definition of Done (7 items)

Items 1–7 in `spec.md` Definition of Done map to the user-story AC above and are all checked `[x]`. No additional unique criteria exist in `spec.md` that are not covered by the user-story AC.

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification | Notes |
|---|-----------|--------|----------|--------------|-------|
| 1 | All projects target `net10.0-windows` | PASS | `framework-targets.2026-04-10T17-35.md`: All 4 .csproj files confirmed `net10.0-windows`. Installed runtimeconfig: Microsoft.NETCore.App 10.0.0. | `dotnet build` verified this review: all projects build as net10.0-windows. | Also confirmed by Windows acceptance Suite A. |
| 2 | Outlook STA thread, acquisition sequence, COM cleanup | PASS | Code inspection: `OutlookStaExecutor` enforces `ApartmentState.STA`. `ScanWorker.ExecuteAsync` routes all scan calls through `sta.InvokeAsync`. `OutlookScanner.ExecuteScanAsync` calls `_com.ReleaseAll` in `finally` block. `EnsureOutlook` handles attach-first, then create-if-autostart flow. | C# tests in `MailBridgeRuntimeTests.OutlookScanner.cs` cover attach, create, COM failure, and cleanup paths. Windows acceptance Suite B confirms bridge reaches `state=ready` with `outlookConnected=true`. | |
| 3 | Inbox scanning with Restrict, poll, overlap, dedupe, normalization | PASS | Code inspection: `ScanInboxFolderAsync` builds `[ReceivedTime] >= ...` filter via `BuildInboxFilter`, applies `Restrict`, iterates with `EnumerateItems` (hard cap), normalizes `MailItem` and `MeetingItem` via `NormalizeMessage`, dedupes by `EntryID` through `bridge_id` upsert. Config defaults: `InboxPollSeconds=30`, `InboxOverlapMinutes=5`. | Tests in `MailBridgeRuntimeTests.OutlookScanner.cs` verify inbox scanning, message normalization, and overlap filter. Windows acceptance Suite C confirms list-messages and get-message return real cached data. | |
| 4 | Calendar scanning with Sort, IncludeRecurrences, bounded filter, no Count, hard cap | PASS | Code inspection: `ScanCalendarFolderAsync` calls `InvokeMember(items, "Sort", "[Start]")`, `SetMemberValue(items, "IncludeRecurrences", true)`, builds bounded `[Start] >= ... AND [Start] < ...` filter. `EnumerateItems` enforces `maxItems` cap. No `.Count` call on recurring view. | Tests in `MailBridgeRuntimeTests.OutlookScanner.cs` verify calendar scanning and event normalization. Windows acceptance Suite C confirms list-calendar and get-event return real cached data. | |
| 5 | Safe/enhanced mode, privacy, no body/attachment logging | PASS | Code inspection: `ResponseShaper.ShapeMessage` nulls out `BodyPreview`, `SenderName`, `SenderEmail` in safe mode. Enhanced mode returns sanitized preview via `BodySanitizer.NormalizePreview`. `ResponseShaper.ShapeEvent` follows same pattern. No `Body` values appear in logger calls. | `ResponseShaperTests.cs` verifies both modes. Windows acceptance Suite D confirms safe-mode privacy assertions pass. | |
| 6 | SQLite persistence complete for messages, events, scan_state | PASS | Code inspection: `CacheRepository.InitializeAsync()` creates all three tables. Upsert methods use `ON CONFLICT DO UPDATE`. List methods use parameterized queries with deterministic ordering. `GetScanStateSnapshotAsync` aggregates scan timestamps. | Repository tests in `MailBridgeRuntimeTests.cs` and `MailBridgeRuntimeTests.Phase5.cs` exercise upsert, list, get, and scan-state operations against in-memory SQLite. | |
| 7 | Named-pipe server: allowed methods, validation, error codes, PipeSecurity, hard fail on SID | PASS | Code inspection: `BuildResponseAsync` validates against `BridgeMethods.All`. Each handler validates params via `RequireParameter`, `RequireIso8601`, `RequireLimit`. `BridgeErrorCodes` surface deterministic codes. `BuildPipeSecurity` resolves 4 identities and denies `NetworkSid`. `AccountSidResolver("openclaw-svc")` throws if resolution fails, preventing pipe creation. | Tests in `MailBridgeRuntimeTests.Phase5.cs` cover RPC validation and error mapping. `windows-operator-validation.2026-04-10T17-22.md` confirms `OpenClawSvcPipeConnect=true` and `NetworkDenyVerified=true`. | |
| 8 | Client: specified commands, JSON stdout, stderr diagnostics, pipe-name resolution, exit codes | PASS | Code inspection: `Client/Program.cs` — `Parse` and `Build` support exactly the specified commands. `RunAsync` writes JSON to `stdout` and errors to `stderr`. `ResolvePipeName` reads settings file with `--pipe-name` override. Exit-code mapping in the switch expression. | `MailBridgeClientTests.cs` verifies parse, build, send, pipe-name resolution, and exit-code mapping. | |
| 9 | Scripts and runbook: install, uninstall, register-task, test-mailbridge, acceptance suites A-F | PASS | `scripts/install-mailbridge.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/test-mailbridge.ps1` exist in the diff. `docs/mailbridge-runbook.md` updated. | 19 Pester tests in `tests/scripts/` cover all four scripts. `windows-acceptance.2026-04-10T17-22.md` confirms suites A, B, C, D, F passed. | Suite E (operator validation) documented separately in `windows-operator-validation.2026-04-10T17-22.md`. |
| 10 | Tests prove actual behavior, not stubs | PASS | 87 C# tests cover: DTO/ID helpers (`BridgeContractsCoverageTests`), config validation, redaction/sanitization (`ResponseShaperTests`), repository persistence/query (`Phase5`), RPC validation/error mapping (`Phase5`), safe vs enhanced modes, stale-cache handling, COM boundary orchestration (`OutlookScanner`), and client behavior (`MailBridgeClientTests`). 19 Pester tests for script assertions. | `dotnet test` passes 87 total (86 passed, 1 platform skip). `mcp_drmcopilotext_run_poshqc_test` passes 19/19. | No placeholder payloads remain; all RPC methods return repository-backed results. |

## Summary

**Overall feature readiness: NEEDS REVISION**

All 10 functional acceptance criteria from `user-story.md` and all 7 Definition of Done items from `spec.md` evaluate as **PASS**. The implementation is functionally complete.

The feature is blocked from merge by structural policy violations identified in the policy audit:

1. **Three files exceed the 500-line limit**: `OutlookScanner.cs` (580), `MailBridgeRuntimeTests.cs` (687), `MailBridgeRuntimeTests.OutlookScanner.cs` (652).
2. **One test harness uses prohibited temp files**: `CodexWebSetupScriptHarness.cs` creates temp directories.
3. **One evidence artifact is inaccurate**: `feature-completion.2026-04-10T17-35.md` reports incorrect test count and coverage.

These are mechanical refactors and evidence corrections that do not require behavioral changes.

## Acceptance Criteria Check-off

All 10 acceptance criteria in `user-story.md` were already checked `[x]` prior to this review. All 7 Definition of Done items in `spec.md` were already checked `[x]`. This review confirms their PASS status; no checkbox changes are needed.

### Acceptance Criteria Status

- Source: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/user-story.md`, `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/spec.md`
- Total AC items (user-story): 10
- Checked off (delivered): 10
- Remaining (unchecked): 0
- Total DoD items (spec): 7
- Checked off (delivered): 7
- Remaining (unchecked): 0
- Items remaining: none
