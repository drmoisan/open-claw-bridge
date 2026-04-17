# com-ui-freeze (Issue #31)

- Date captured: 2026-04-17
- Author: drmoisan
- Status: Promoted -> docs/features/active/com-ui-freeze/ (Issue #31)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #31
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/31
- Last Updated: 2026-04-17
- Work Mode: minor-audit

## Summary

Outlook COM interaction in the MailBridge scanner freezes the Outlook UI for extended periods. The scanner makes rapid sequential COM calls on a dedicated STA thread to enumerate inbox and calendar items, and each call is marshaled to Outlook's UI thread via cross-apartment COM. Processing up to 500 items per scan without yielding monopolizes Outlook's UI thread, starving window message pumping and causing visible freezes. This may also cause contention with the TaskMaster VSTO add-in, which shares the same Outlook COM apartment.

## Environment

- OS/version: Windows 11
- Runtime: .NET 8
- Outlook: Classic Outlook (desktop, COM-based)
- Coexisting add-ins: TaskMaster VSTO (https://github.com/drmoisan/TaskMaster)

## Steps to Reproduce

1. Start the MailBridge service while Outlook is running with >50 inbox items since last scan.
2. Observe Outlook UI during the scan cycle (default 30-second inbox poll).
3. Outlook UI becomes unresponsive for several seconds during each scan.
4. If TaskMaster VSTO is also active, the freeze is more pronounced due to COM thread contention.

## Expected Behavior

Outlook UI remains responsive during MailBridge scan operations.

## Actual Behavior

Outlook UI locks up for noticeable periods (several seconds to tens of seconds) during each inbox or calendar scan cycle because the scanner makes hundreds of cross-apartment COM calls without yielding.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet: No error logs; the hang is a performance/contention issue, not a crash.

## Impact / Severity

- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

## Suspected Cause / Notes

Root cause: `OutlookScanner.ScanInboxFolderAsync` and `ScanCalendarFolderAsync` iterate through all restricted items in a tight loop, making ~10+ COM property reads per item (EntryID, Subject, ReceivedTime, SenderName, SenderEmailAddress, Body, etc.). Each property read is a cross-apartment COM call that runs on Outlook's UI thread. With MaxItemsPerScan=500, this can produce thousands of consecutive COM calls without any yield, completely blocking Outlook's message pump.

Key files: `OutlookScanner.cs` (item loops), `ScanWorker.cs` (STA invocation), `BridgeContracts.cs` (settings).

The `ScanWorker` wraps the entire scan in a single `sta.InvokeAsync()` lambda using `.GetAwaiter().GetResult()`, making the whole scan one atomic blocking operation on the STA thread.

## Proposed Fix / Validation Ideas

- [x] Add a configurable COM yield batch size (e.g., 25 items) and yield delay (e.g., 15ms) to `BridgeSettings`
- [x] Insert `Thread.Sleep(yieldMs)` on the STA thread between batches during item enumeration to allow Outlook's UI thread to process messages
- [ ] Unit coverage: verify yielding behavior with batch boundary tests
- [ ] Manual verification: confirm UI responsiveness during scans with TaskMaster active

## Acceptance Criteria

- [x] `BridgeSettings` includes a `ComYieldBatchSize` (default 25) and `ComYieldMilliseconds` (default 15) setting
- [x] `OutlookScanner` yields control (via `Thread.Sleep`) after every `ComYieldBatchSize` items during inbox and calendar scans
- [x] Existing unit tests continue to pass with no regressions
- [x] New unit tests verify that yield logic is invoked at correct batch boundaries
- [x] `BridgeSettingsValidator` rejects invalid yield settings (batch size < 1, yield ms < 0)

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch