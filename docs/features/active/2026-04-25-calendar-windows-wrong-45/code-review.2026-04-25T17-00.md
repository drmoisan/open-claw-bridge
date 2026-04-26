# Code Review — Issue #45: Calendar Windows Wrong (UTC Double-Shift)

Timestamp: 2026-04-25T17-00
Reviewer: feature-review-agent
Work Mode: full-bug
Branch: feature/20260425175224-calendar-windows-wrong

Note: The C# implementation changes reviewed here exist as working-tree modifications and new untracked files. They are not yet committed. All code is evaluated as authored; commit status is a policy finding in the policy audit, not repeated here.

---

## File: `src/OpenClaw.MailBridge/OutlookComHelpers.cs` (lines 105–132)

### Added Method: `GetOptionalUtcDateTimeOffset`

**Correctness of root cause fix**

The original `GetOptionalDateTimeOffset` uses `dateTime.ToUniversalTime()` unconditionally for `DateTime` inputs. For `DateTimeKind.Unspecified` returned by Outlook's `StartUTC`/`EndUTC` COM properties, this incorrectly adds the local UTC offset to a value that is already UTC.

The new method handles this correctly:

- `DateTimeKind.Unspecified` arm: `new DateTimeOffset(dateTime, TimeSpan.Zero)` — treats the value as-is with zero offset. This is the correct behavior for Outlook UTC properties.
- `DateTimeKind.Utc` arm: `new DateTimeOffset(dateTime)` — passthrough for already-UTC values.
- `DateTimeKind.Local` arm: `new DateTimeOffset(dateTime.ToUniversalTime())` — converts local to UTC.
- `DateTimeOffset` arm: `.ToUniversalTime()` — normalizes non-zero-offset `DateTimeOffset` values.
- String fallback: `DateTimeOffset.TryParse` — consistent with the existing method.
- Null default: `null` — consistent with the existing method.

The switch expression is ordered correctly. The `DateTimeKind.Utc` and `DateTimeKind.Local` arms use property patterns and appear before the catch-all `DateTime dateTime` arm (Unspecified). This ordering is required for the switch to dispatch correctly; it is correctly implemented.

Verdict: **The fix is correct and complete for the stated bug.**

**Design consistency**

The new method mirrors `GetOptionalDateTimeOffset` exactly in structure (same try/catch, same method signature pattern, same visibility). This is appropriate — the two methods form a matched pair for local-time and UTC COM properties respectively.

**XML documentation**

The `<summary>` comment explicitly states that `Unspecified` kind is treated as already UTC and explains the reason (Outlook `StartUTC`/`EndUTC` double-shift). This is adequate and follows policy for non-obvious contracts.

**Minor observation — DateTimeOffset arm behavior**

The `DateTimeOffset dto => dto.ToUniversalTime()` arm normalizes to UTC+0. This is different from `GetOptionalDateTimeOffset` which returns `dto` without normalization (`DateTimeOffset dto => dto`). The difference is intentional: the UTC method guarantees the result has zero offset regardless of input. No defect, but the behavioral difference is worth noting in case callers depend on preserving the original offset.

---

## File: `src/OpenClaw.MailBridge/OutlookScanner.cs` (lines 422–427)

### Updated Method: `NormalizeEvent`

The substitution of `GetOptionalUtcDateTimeOffset` for `StartUTC`/`EndUTC` and retention of `GetOptionalDateTimeOffset` for `Start`/`End` is correct.

```csharp
var startUtc =
    OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "StartUTC")
    ?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "Start");
var endUtc =
    OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "EndUTC")
    ?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "End");
```

The fallback from UTC property to local-time property is preserved, which is correct: if `StartUTC` is unavailable (null return from the COM accessor), fall back to `Start` and apply local-to-UTC conversion via the existing method.

No other callers of `GetOptionalDateTimeOffset` in `OutlookScanner.cs` were changed (the `ReceivedTime`/`SentOn` assignments at lines 397–398 are email-path calls and correctly remain unchanged).

**OutlookScanner.cs line count: 495** — within the 500-line limit but close. No new lines were added beyond the two call-site substitutions; the count remained the same (4 changed lines, 4 deletions, 4 insertions net zero).

---

## File: `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs`

### Class: `OutlookComHelpersDateTimeKindTests`

**Test coverage**

All six required test methods are present and correctly implemented:

1. `GetOptionalUtcDateTimeOffset_UnspecifiedKind_returns_offset_with_zero_offset` — uses `DateTimeKind.Unspecified`, asserts `UtcDateTime == 2026-04-27T17:00:00Z` and `Offset == TimeSpan.Zero`. This is the canonical verification from the issue's Verification Notes section.

2. `GetOptionalUtcDateTimeOffset_UtcKind_returns_same_utc_value` — asserts UTC passthrough, zero offset.

3. `GetOptionalUtcDateTimeOffset_LocalKind_converts_to_utc` — asserts `UtcDateTime == dt.ToUniversalTime()` (machine-timezone-independent comparison), zero offset.

4. `GetOptionalUtcDateTimeOffset_DateTimeOffset_returns_utc` — passes `DateTimeOffset(2026, 4, 27, 13, 0, 0, TimeSpan.FromHours(-4))`, asserts `UtcDateTime == 2026-04-27T17:00:00Z`. Correctly verifies the UTC normalization of a non-zero-offset input.

5. `GetOptionalUtcDateTimeOffset_MissingMember_returns_null` — calls with `"NonExistentMember"`, asserts null. Tests the catch-block behavior.

6. `GetOptionalUtcDateTimeOffset_StringValue_parses_to_utc` — passes ISO-8601 string, asserts non-null. Tests the `TryParse` fallback.

**Arrange–Act–Assert structure**: All tests follow AAA with inline comments. Clear.

**Helper classes**: Three private nested helper classes (`FakeDateTimeHolder`, `FakeDateTimeOffsetHolder`, `FakeStringHolder`) with a single property each. These back the COM reflection calls without any real I/O or filesystem access. Correct approach per policy.

**Minor observation**: `GetOptionalUtcDateTimeOffset_LocalKind_converts_to_utc` asserts `result.Value.Offset.Should().Be(TimeSpan.Zero)`. The Local-kind arm constructs `new DateTimeOffset(dateTime.ToUniversalTime())`. When `dateTime.ToUniversalTime()` returns a `DateTime` with `DateTimeKind.Utc`, `new DateTimeOffset(utcDt)` sets offset to zero. The assertion is correct. However, the test does not pin the UTC value to a specific expected constant — it compares against `dt.ToUniversalTime()` which is evaluated at test runtime using the machine's local timezone. This makes the assertion correct in all timezone environments (it is not testing the value itself but the round-trip), which is the right design for a Local-kind test.

---

## File: `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs`

### Class: `OutlookScannerCalendarUtcTests`

**Test: `ScanCalendarAsync_StartUTC_UnspecifiedKind_stores_correct_utc`**

This is an integration-level test that exercises `OutlookScanner.ScanCalendarAsync` through the full scanning pipeline using a fake COM object.

**Setup correctness**: The `FakeAppointmentItem` is configured with:
- `StartUTC = new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Unspecified)` — the exact bug scenario.
- `EndUTC = new DateTime(2026, 4, 27, 18, 0, 0, DateTimeKind.Unspecified)` — paired correctly.
- `Start` and `End` are set to `FixedNow.AddDays(1)` — non-UTC fallback values also populated, which is correct because `NormalizeEvent` requires both to be present.

**Assertion correctness**: The test asserts:
- `evt.StartUtc.UtcDateTime == new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc)` — the exact value that would be wrong on an EDT machine before the fix (would be 21:00 UTC).
- `evt.StartUtc.Offset == TimeSpan.Zero` — confirms zero offset.

The reason message "Unspecified-kind UTC value from Outlook COM must not be double-shifted" provides clear failure context.

**Scanner construction**: Uses `BuildScanner` helper pattern consistent with `OutlookScannerResponseStatusTests`. `BridgeStateStore` and `NullLogger` are used, no external services. No filesystem or network access.

**Independence**: `FixedNow` is a fixed `DateTimeOffset` — no `DateTime.Now` calls. The test is deterministic.

---

## File: `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`

Two new properties added to `FakeAppointmentItem`:
```csharp
public DateTime? StartUTC { get; init; }
public DateTime? EndUTC { get; init; }
```

These are nullable `DateTime?` properties rather than `DateTimeOffset?` because Outlook COM returns raw `DateTime` values for these properties (the bug is precisely that Outlook does not set `Kind` correctly). Nullable type is correct: existing tests that create `FakeAppointmentItem` without these properties will get `null`, causing `GetOptionalUtcDateTimeOffset` to fall through to the `?? GetOptionalDateTimeOffset(item, "Start")` fallback — no regression to existing tests.

---

## File: `scripts/Uninstall.ps1`

The change gates Stage 5 (install-record deletion) behind `if ($failures.Count -eq 0)`. This is correct behavior: preserving the install record after partial failure allows a retry to use the recorded metadata.

**Code quality**: Clean and readable. The `else` branch logs a clear informational message identifying why the record was preserved. `ShouldProcess` is maintained for the record-deletion step. The `$failures` check is correct and minimal.

**No scope creep**: Only Stage 5 was restructured. Stages 1–4 and Stage 6 are unchanged.

---

## File: `tests/scripts/Uninstall.Tests.ps1`

Test updates correctly reflect the new behavior:

1. Test "runs the install-record-remove step even when earlier stages fail" → renamed to match new behavior: "preserves the install record when destination-remove fails". The assertion was changed from `Should -BeGreaterThan 0` (record was attempted) to `Should -Be 0` (record was not touched). This is an accurate behavioral assertion.

2. Second test now verifies that the destination path cleanup still runs (at least 1 non-record Remove-Item path) while the install-record path count is 0.

The test changes are consistent with the production behavior change and accurately verify the intended contract.

---

## File: `docs/mailbridge-runbook.md`

**Defect (blocking)**: Line 199 contains a syntax error in a PowerShell code block:
```
$versionNum = '1.0.1.3`
```
The string is opened with `'` (single-quote) but closed with `` ` `` (backtick). In PowerShell, this is an unterminated string literal; executing the block verbatim will raise a parse error. The correct line is:
```
$versionNum = '1.0.1.3'
```

The remaining runbook changes (replacing hardcoded paths with `$versionNum` variable references and using `& (Join-Path ...)` syntax) are correct improvements that make the runbook more portable. All other changes are accurate.

---

## Summary

| File | Verdict | Notes |
|---|---|---|
| `OutlookComHelpers.cs` — new method | PASS | Correct fix, all arms present, doc comment adequate |
| `OutlookScanner.cs` — call sites | PASS | Correct substitution, fallbacks preserved |
| `OutlookComHelpersDateTimeKindTests.cs` | PASS | All 6 required tests present, AAA, deterministic |
| `OutlookScannerCalendarUtcTests.cs` | PASS | Pipeline test correct, pinned assertion, no I/O |
| `MailBridgeRuntimeTestDoubles.cs` | PASS | Nullable DateTime? properties correct |
| `Uninstall.ps1` | PASS | Conditional record-delete correct |
| `Uninstall.Tests.ps1` | PASS | Test behavior assertions updated correctly |
| `mailbridge-runbook.md` | FAIL | Backtick syntax error in PowerShell code block (line 199) |
