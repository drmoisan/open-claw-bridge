---
Timestamp: 2026-04-25T00-00
---

# AC-1 Verification: GetOptionalUtcDateTimeOffset

## Criterion

`GetOptionalUtcDateTimeOffset` exists in `src/OpenClaw.MailBridge/OutlookComHelpers.cs` with all four `DateTime` kind branches and the `Unspecified` arm using `TimeSpan.Zero`.

## Verification

File: `src/OpenClaw.MailBridge/OutlookComHelpers.cs`, lines 111-132.

Switch arms present:
- Line 118: `DateTimeOffset dto => dto.ToUniversalTime()` — DateTimeOffset arm.
- Line 119: `DateTime { Kind: DateTimeKind.Utc } dateTime => new DateTimeOffset(dateTime)` — Utc arm.
- Lines 120-122: `DateTime { Kind: DateTimeKind.Local } dateTime => new DateTimeOffset(dateTime.ToUniversalTime())` — Local arm.
- Line 123: `DateTime dateTime => new DateTimeOffset(dateTime, TimeSpan.Zero)` — Unspecified (catch-all DateTime) arm using TimeSpan.Zero.
- Line 124: TryParse fallback.
- Line 125: null default.

XML summary doc comment present at lines 105-110. Method is `internal static`.

AC-1: PASS.
