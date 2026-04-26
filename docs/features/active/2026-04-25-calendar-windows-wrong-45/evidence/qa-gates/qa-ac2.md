---
Timestamp: 2026-04-25T00-00
---

# AC-2 Verification: NormalizeEvent Calls

## Criterion

`NormalizeEvent` in `src/OpenClaw.MailBridge/OutlookScanner.cs` calls `GetOptionalUtcDateTimeOffset` for `StartUTC`/`EndUTC` and `GetOptionalDateTimeOffset` for `Start`/`End`.

## Verification

File: `src/OpenClaw.MailBridge/OutlookScanner.cs`

- Line 423: `OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "StartUTC")` — CORRECT.
- Line 424: `?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "Start")` — CORRECT (fallback).
- Line 426: `OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "EndUTC")` — CORRECT.
- Line 427: `?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "End")` — CORRECT (fallback).

Other calls to `GetOptionalDateTimeOffset` at lines 397-398 are for `ReceivedTime`/`SentOn` in the email path — unrelated and correctly unchanged.

AC-2: PASS.
