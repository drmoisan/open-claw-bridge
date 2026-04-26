---
Timestamp: 2026-04-25T00-00
---

# AC-3 Verification: Test Files

## Criterion

`OutlookComHelpersDateTimeKindTests.cs` contains tests for all three `DateTime` kind branches (Unspecified, Utc, Local) plus the DateTimeOffset case. `OutlookScannerCalendarUtcTests.cs` contains the scanner integration test with `Unspecified` kind input.

## Verification

### OutlookComHelpersDateTimeKindTests.cs

File: `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs`

Tests present:
1. Line 16: `GetOptionalUtcDateTimeOffset_UnspecifiedKind_returns_offset_with_zero_offset` — Unspecified kind.
2. Line 34: `GetOptionalUtcDateTimeOffset_UtcKind_returns_same_utc_value` — Utc kind.
3. Line 52: `GetOptionalUtcDateTimeOffset_LocalKind_converts_to_utc` — Local kind.
4. Line 68: `GetOptionalUtcDateTimeOffset_DateTimeOffset_returns_utc` — DateTimeOffset input.
5. Line 85: `GetOptionalUtcDateTimeOffset_MissingMember_returns_null` — missing COM member.
6. Line 98: `GetOptionalUtcDateTimeOffset_StringValue_parses_to_utc` — string parse fallback.

All 6 required test methods present. Uses private nested `FakeDateTimeHolder`, `FakeDateTimeOffsetHolder`, `FakeStringHolder` helpers. No real I/O or filesystem access.

### OutlookScannerCalendarUtcTests.cs

File: `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs`

Test present:
1. Line 39: `ScanCalendarAsync_StartUTC_UnspecifiedKind_stores_correct_utc` — scanner integration test with `DateTimeKind.Unspecified` `StartUTC`/`EndUTC` input.

AC-3: PASS.
