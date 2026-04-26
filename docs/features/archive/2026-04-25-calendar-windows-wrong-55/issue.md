# Calendar Windows Wrong: UTC Double-Shift in OutlookComHelpers (Issue #55)

- Date captured: 2026-04-25
- Author: drmoisan
- Status: Promoted -> docs/features/active/Calendar_Windows_Wrong_UTC_Double-Shift_in_OutlookComHelpers/ (Issue #55)
- Issue: #55
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/55
- Last Updated: 2026-04-26

## Summary

`OutlookComHelpers.GetOptionalDateTimeOffset` calls `.ToUniversalTime()` on every
`DateTime` with `DateTimeKind.Unspecified` returned by Outlook COM. This is correct for
the `Start`/`End` properties, which carry local time. However, Outlook's `StartUTC` and
`EndUTC` properties return the UTC time, also with `DateTimeKind.Unspecified`. Calling
`.ToUniversalTime()` on that value treats it as local time and adds the machine UTC offset
a second time — a double-shift.

On an EDT (UTC-4) host, a meeting at 9:00 AM EDT (true UTC 13:00) is stored as 17:00 UTC
(1:00 PM EDT appearance). The OpenClaw assistant then computes free windows against the
wrong times and returns defective scheduling recommendations.

Issue originally filed: https://github.com/drmoisan/drm-copilot/issues/45

## Root Cause

File: `src/OpenClaw.MailBridge/OutlookComHelpers.cs`

```csharp
DateTime dateTime => new DateTimeOffset(dateTime.ToUniversalTime()),
```

Used for both local-time properties (`Start`, `End`) and UTC properties (`StartUTC`,
`EndUTC`). For UTC properties, `DateTimeKind.Unspecified` means "already UTC" but
`.ToUniversalTime()` treats it as "local", adding the offset again.

## Acceptance Criteria

- [x] AC-1: Add `GetOptionalUtcDateTimeOffset` to `OutlookComHelpers` that treats
  `DateTimeKind.Unspecified` DateTime as already UTC (wraps with `TimeSpan.Zero`),
  `DateTimeKind.Utc` as-is, and `DateTimeKind.Local` by applying `.ToUniversalTime()`.
- [x] AC-2: `NormalizeEvent` in `OutlookScanner` uses `GetOptionalUtcDateTimeOffset`
  for `StartUTC`/`EndUTC` and retains `GetOptionalDateTimeOffset` for `Start`/`End`.
- [x] AC-3: New unit tests covering all three `DateTime` kind branches of
  `GetOptionalUtcDateTimeOffset` (Unspecified, Utc, Local), and a test that exercises
  `NormalizeEvent` through the full scanner pipeline using a fake item whose `StartUTC`
  property returns an `Unspecified` `DateTime`.
- [x] AC-4: Full toolchain passes (CSharpier → MSBuild analyzers → nullable build →
  dotnet test) with no new failures and repository-wide coverage >= 80%.

## Verification Notes

After the fix: on any machine timezone, `GetOptionalUtcDateTimeOffset` called with
`DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Unspecified)` must return a
`DateTimeOffset` with `UtcDateTime == 2026-04-27T17:00:00Z` and `Offset == TimeSpan.Zero`,
regardless of the local machine timezone.

## Severity

High — the defect causes the assistant to report meeting times 4 hours later than
actual on EDT hosts, making all availability recommendations inaccurate.
