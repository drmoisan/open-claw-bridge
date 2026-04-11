---
title: "release-individual-com-items - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-41"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# release-individual-com-items (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

The `EnumerateItems` iterator ([OutlookScanner.cs:341-362](src/OpenClaw.MailBridge/OutlookScanner.cs#L341-L362)) yields each COM item object to its caller and then moves on to the next one. Neither `EnumerateItems` nor its two callers — the `foreach` loops in `ScanInboxFolderAsync` ([OutlookScanner.cs:218-229](src/OpenClaw.MailBridge/OutlookScanner.cs#L218-L229)) and `ScanCalendarFolderAsync` ([OutlookScanner.cs:273-286](src/OpenClaw.MailBridge/OutlookScanner.cs#L273-L286)) — call `_com.Release(item)` after the item has been normalized and upserted. The `Items` collection and `Restrict`-filtered collection are released in `finally` blocks via `_com.ReleaseAll(restrictedItems, items)`, but the individual `MailItem` and `AppointmentItem` RCWs obtained by enumerating those collections are not explicitly released.

Each unreleased RCW holds a reference count on the underlying COM object, which Outlook tracks. Under sustained polling — multiple inbox and calendar scans over the bridge's lifetime — these accumulated RCW references prevent Outlook from reclaiming those COM objects promptly, constituting a COM handle leak. The design audit's Suite F acceptance test is specifically intended to detect this class of problem but is not yet exercised at sufficient scale (25 iterations vs. the required 100).

This is listed as deviation #14 (Medium) in the design audit.

## Proposed Behavior

After each item is normalized and processed in the `foreach` loops of `ScanInboxFolderAsync` and `ScanCalendarFolderAsync`, call `_com.Release(item)` to decrement the RCW reference count on that COM object immediately.

The release must happen in the calling scan methods, not inside `EnumerateItems`, because the iterator has already yielded the item — releasing it inside the generator body would risk releasing an object the caller is still using. The correct pattern is:

```csharp
foreach (var item in EnumerateItems(restrictedItems, _settings.MaxItemsPerScan))
{
    try
    {
        var message = NormalizeMessage(item);
        if (message is null)
        {
            continue;
        }
        await repo.UpsertMessageAsync(message.EntryId, message.StoreId, message.Dto);
        processedCount++;
    }
    finally
    {
        _com.Release(item);
    }
}
```

The `finally` block ensures the item is released even if `NormalizeMessage` or `UpsertMessageAsync` throws, preventing a leak on the error path as well.

The `_com.Release` method already handles `null` and non-COM objects gracefully (it checks `Marshal.IsComObject` before calling `Marshal.FinalReleaseComObject`) so no null-guard is needed at the call site.

## Acceptance Criteria (early draft)

- [ ] `ScanInboxFolderAsync` calls `_com.Release(item)` for every item yielded by `EnumerateItems`, whether the item was successfully normalized or `NormalizeMessage` returned `null`.
- [ ] `ScanCalendarFolderAsync` calls `_com.Release(item)` for every item yielded by `EnumerateItems`, whether the item was successfully normalized or `NormalizeEvent` returned `null`.
- [ ] The release call is in a `finally` block so that it executes even when `NormalizeMessage`, `NormalizeEvent`, or the `UpsertAsync` call throws an exception.
- [ ] The `EnumerateItems` iterator itself is not modified — it continues to yield items without releasing them.
- [ ] `_com.ReleaseAll(restrictedItems, items)` in the existing `finally` blocks is not removed; it continues to release the collection objects after the loop completes.
- [ ] Unit tests that mock `ComActiveObject` can verify that `Release` is called once per yielded item.

## Constraints & Risks

- `Marshal.FinalReleaseComObject` releases all remaining reference counts on the RCW in one call, regardless of how many times `AddRef` was called. If any other reference to the same `item` object exists after the `finally` block executes, accessing it will throw a `COMException` or return garbage. `NormalizeMessage` and `NormalizeEvent` must complete all COM property reads before control reaches the `finally`. As currently written, normalization is synchronous and fully contained within the method bodies, so this is safe.
- The `EnumerateItems` iterator uses `yield return`, which means the item object is passed to the caller by reference. After yielding, `EnumerateItems` suspends and the MoveNext call on the next iteration will not re-access the prior item. There is no re-use of the yielded item inside the iterator.
- Calling `_com.Release(item)` on a non-COM object (e.g., a plain .NET object from a fake items collection in tests) is safe — `ComActiveObject.Release` checks `Marshal.IsComObject` before calling `FinalReleaseComObject`.
- This fix cannot be verified in the automated test environment because it requires a live Outlook COM instance to observe the RCW reference count drop. The existing fake items collection in tests (`FakeMailItem`, `FakeAppointmentItem`) are plain .NET objects, not COM RCWs. Unit tests can verify that `_com.Release` is called the correct number of times via a mock, but cannot confirm that handle counts drop in a real Outlook process.
- A fix to the COM hygiene acceptance test (Suite F) — increasing iterations from 25 to 100 and adding process/handle monitoring — is a related but separate work item (`release-individual-com-items` addresses the source of the leak; the Suite F fix validates it at the acceptance level).

## Test Conditions to Consider

- [ ] Unit test: scan inbox with a mock `ComActiveObject` and a fake items collection containing 3 items; assert `_com.Release` is called exactly 3 times after `ScanInboxFolderAsync` completes.
- [ ] Unit test: scan calendar with a mock `ComActiveObject` and a fake items collection containing 2 items; assert `_com.Release` is called exactly 2 times after `ScanCalendarFolderAsync` completes.
- [ ] Unit test: when `NormalizeMessage` returns `null` for an item (e.g., missing `EntryID`), assert `_com.Release` is still called for that item.
- [ ] Unit test: when `NormalizeEvent` returns `null` for an item, assert `_com.Release` is still called.
- [ ] Unit test: when `UpsertMessageAsync` throws, assert `_com.Release` is still called for the item being processed at the time of the throw.
- [ ] Unit test: `_com.ReleaseAll(restrictedItems, items)` continues to be called in the `finally` block after the loop — verify it is not inadvertently removed.
- [ ] Integration scenario (manual, requires live Outlook): run the bridge for 100 consecutive scan cycles (Suite F); confirm that the Outlook process handle count and RCW count do not grow unboundedly between cycles.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/release-individual-com-items/` folder from the template

