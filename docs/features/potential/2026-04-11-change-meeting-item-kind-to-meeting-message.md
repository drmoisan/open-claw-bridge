# change-meeting-item-kind-to-meeting-message (Potential Bug)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

## Summary

Meeting request items returned by `list_recent_meeting_requests` carry `item_kind = "meeting"` instead of the spec-required `"meeting_message"`, causing callers that key on this discriminator to misclassify or fail to identify meeting items. The same incorrect string is hardcoded in the SQL filter that drives the endpoint, so fixing the normalization without fixing the filter (or vice versa) would silently break the query.

## Environment

- OS/version: Windows (net10.0-windows target)
- Runtime: .NET 10
- Component: `NormalizeMessage` in [OutlookScanner.cs:387](src/OpenClaw.MailBridge/OutlookScanner.cs#L387) and `ListRecentMeetingRequestsAsync` SQL literal in [CacheRepository.cs:194](src/OpenClaw.MailBridge/CacheRepository.cs#L194)
- Data source: Outlook inbox scan (COM) and SQLite messages cache

## Steps to Reproduce

1. Start the bridge with at least one meeting request in the inbox.
2. Issue a `list_recent_meeting_requests` pipe request.
3. Inspect the `item_kind` field of any returned item.

## Expected Behavior

Each returned item has `"item_kind": "meeting_message"`.

## Actual Behavior

Each returned item has `"item_kind": "meeting"`. The value `"meeting"` is assigned at [OutlookScanner.cs:387](src/OpenClaw.MailBridge/OutlookScanner.cs#L387) by the ternary expression `isMeeting ? "meeting" : "mail"`, written to the cache, and then filtered against by the hardcoded SQL literal `item_kind = 'meeting'` at [CacheRepository.cs:194](src/OpenClaw.MailBridge/CacheRepository.cs#L194).

No error is raised; the wrong string is stored, returned, and filtered consistently — which is why the endpoint appears functional in isolation while producing non-spec output.

## Logs / Screenshots

- [ ] Attached minimal logs or screenshot
- Snippet: No log output records the `item_kind` value; the defect is visible only by inspecting the JSON response payload or the SQLite `messages` table.

## Impact / Severity

- [ ] Blocker
- [x] High
- [ ] Medium
- [ ] Low

Listed as deviation #4 (Medium) in the design audit. Severity is elevated to High because `item_kind` is the primary discriminator for meeting-vs-mail classification in the API contract; callers that switch on this value will misroute all meeting items.

## Suspected Cause / Notes

The string `"meeting"` was used during initial implementation and was never reconciled against the spec value `"meeting_message"`. There are two independent locations where it must be corrected atomically:

1. **`OutlookScanner.NormalizeMessage` ([OutlookScanner.cs:387](src/OpenClaw.MailBridge/OutlookScanner.cs#L387))** — the ternary that writes `item_kind` into the `MessageDto`:

   ```csharp
   // current
   isMeeting ? "meeting" : "mail"

   // required
   isMeeting ? "meeting_message" : "mail"
   ```

2. **`CacheRepository.ListRecentMeetingRequestsAsync` ([CacheRepository.cs:194](src/OpenClaw.MailBridge/CacheRepository.cs#L194))** — the SQL predicate that filters the cache:

   ```sql
   -- current
   AND item_kind = 'meeting'

   -- required
   AND item_kind = 'meeting_message'
   ```

Because both the write path and the read path use the same string independently, they must be changed together. Changing only the normalization would cause `ListRecentMeetingRequestsAsync` to return zero rows (filter matches the old string, which no longer exists in new rows). Changing only the SQL filter would cause it to return zero rows (no rows with the new string exist in the cache until a re-scan).

There are also three test locations that hardcode `"meeting"` and will need to be updated alongside the production fix:

- [MailBridgeRuntimeTests.OutlookScanner.cs:280](tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs#L280) — asserts `msg.ItemKind.Should().Be("meeting")`
- [MailBridgeRuntimeTests.Calendar.cs:316](tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Calendar.cs#L316) — asserts `repo.Messages.Values.First().ItemKind.Should().Be("meeting")`
- [MailBridgeRuntimeTestDoubles.cs:218](tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs#L218) — fake repo filters on `x.ItemKind == "meeting"`

Introducing a named constant (e.g., `ItemKinds.MeetingMessage`) shared between `OutlookScanner`, `CacheRepository`, and the test doubles would prevent this class of drift from recurring, but is not required for the fix itself.

After the fix, existing rows in deployed SQLite caches will still carry `item_kind = 'meeting'`. `ListRecentMeetingRequestsAsync` will return zero results until those rows are refreshed by a subsequent inbox scan. No migration is required; the scan naturally overwrites existing rows via `ON CONFLICT` upsert.

## Proposed Fix / Validation Ideas

- [x] Unit test: `NormalizeMessage` with a `FakeMeetingItem` (type name or `MessageClass` triggers `IsMeetingItem`) produces a `MessageDto` with `ItemKind == "meeting_message"`.
- [x] Unit test: `NormalizeMessage` with a plain mail item produces `ItemKind == "mail"` (no regression).
- [x] Unit test (CacheRepository): upsert a `MessageDto` with `ItemKind = "meeting_message"`, then call `ListRecentMeetingRequestsAsync`; verify the row is returned.
- [x] Unit test (CacheRepository): upsert a `MessageDto` with `ItemKind = "mail"`, then call `ListRecentMeetingRequestsAsync`; verify it is not returned.
- [x] Update existing assertions at [MailBridgeRuntimeTests.OutlookScanner.cs:280](tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.OutlookScanner.cs#L280), [MailBridgeRuntimeTests.Calendar.cs:316](tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Calendar.cs#L316), and the fake repo filter at [MailBridgeRuntimeTestDoubles.cs:218](tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs#L218) from `"meeting"` to `"meeting_message"`.
- [ ] Integration scenario: after a scan that ingests at least one meeting request, issue `list_recent_meeting_requests` and confirm each item's `item_kind` is `"meeting_message"`.
- [ ] Manual verification: confirm that existing cache rows with `item_kind = 'meeting'` are overwritten to `'meeting_message'` after the next inbox scan completes (not required immediately, but should be confirmed before closing the issue).

## Next Step

- [ ] Promote to GitHub issue (bug-report template)
- [ ] Move to active fix folder / branch
