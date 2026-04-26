# Feature Audit — Issue #45: Calendar Windows Wrong (UTC Double-Shift)

Timestamp: 2026-04-25T17-00
Reviewer: feature-review-agent
Work Mode: full-bug
AC Source: issue.md (full-bug mode; no spec.md present — falling back to issue.md AC section)
Branch: feature/20260425175224-calendar-windows-wrong

---

## Acceptance Criteria Evaluation

### AC-1
**Criterion**: Add `GetOptionalUtcDateTimeOffset` to `OutlookComHelpers` that treats `DateTimeKind.Unspecified` DateTime as already UTC (wraps with `TimeSpan.Zero`), `DateTimeKind.Utc` as-is, and `DateTimeKind.Local` by applying `.ToUniversalTime()`.

**Status**: PASS

**Evidence**:
- `src/OpenClaw.MailBridge/OutlookComHelpers.cs` lines 111–132 contain `internal static DateTimeOffset? GetOptionalUtcDateTimeOffset(object target, string memberName)`.
- Switch arms verified directly from source:
  - `DateTimeOffset dto => dto.ToUniversalTime()` — line 118
  - `DateTime { Kind: DateTimeKind.Utc } dateTime => new DateTimeOffset(dateTime)` — line 119
  - `DateTime { Kind: DateTimeKind.Local } dateTime => new DateTimeOffset(dateTime.ToUniversalTime())` — lines 120–122
  - `DateTime dateTime => new DateTimeOffset(dateTime, TimeSpan.Zero)` — line 123 (Unspecified catch-all)
  - TryParse fallback — line 124
  - Null default — line 125
- XML `<summary>` doc comment at lines 105–110 states that Unspecified kind is treated as already UTC.
- Method signature is `internal static` as required.
- Evidence artifact: `evidence/qa-gates/qa-ac1.md` — PASS.

**Checked off in issue.md**: Already marked `[x]` by implementation agent. Confirmed correct by this review.

---

### AC-2
**Criterion**: `NormalizeEvent` in `OutlookScanner` uses `GetOptionalUtcDateTimeOffset` for `StartUTC`/`EndUTC` and retains `GetOptionalDateTimeOffset` for `Start`/`End`.

**Status**: PASS

**Evidence**:
- `src/OpenClaw.MailBridge/OutlookScanner.cs` lines 422–427 verified directly:
  - Line 423: `OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "StartUTC")`
  - Line 424: `?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "Start")` (fallback)
  - Line 426: `OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "EndUTC")`
  - Line 427: `?? OutlookComHelpers.GetOptionalDateTimeOffset(item, "End")` (fallback)
- The `ReceivedTime`/`SentOn` calls at lines 397–398 remain as `GetOptionalDateTimeOffset` — unrelated and correctly unchanged.
- Evidence artifact: `evidence/qa-gates/qa-ac2.md` — PASS.

**Checked off in issue.md**: Already marked `[x]`. Confirmed correct.

---

### AC-3
**Criterion**: New unit tests covering all three `DateTime` kind branches of `GetOptionalUtcDateTimeOffset` (Unspecified, Utc, Local), and a test that exercises `NormalizeEvent` through the full scanner pipeline using a fake item whose `StartUTC` property returns an `Unspecified` `DateTime`.

**Status**: PASS

**Evidence**:
- `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs` — 6 tests:
  1. `GetOptionalUtcDateTimeOffset_UnspecifiedKind_returns_offset_with_zero_offset` — Unspecified branch
  2. `GetOptionalUtcDateTimeOffset_UtcKind_returns_same_utc_value` — Utc branch
  3. `GetOptionalUtcDateTimeOffset_LocalKind_converts_to_utc` — Local branch
  4. `GetOptionalUtcDateTimeOffset_DateTimeOffset_returns_utc` — DateTimeOffset branch
  5. `GetOptionalUtcDateTimeOffset_MissingMember_returns_null` — missing member / catch branch
  6. `GetOptionalUtcDateTimeOffset_StringValue_parses_to_utc` — TryParse branch
- `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs` — 1 test:
  - `ScanCalendarAsync_StartUTC_UnspecifiedKind_stores_correct_utc` — exercises `ScanCalendarAsync` with `StartUTC = DateTimeKind.Unspecified`, asserts `StartUtc.UtcDateTime == 2026-04-27T17:00:00Z`.
- `FakeAppointmentItem` has `public DateTime? StartUTC { get; init; }` and `public DateTime? EndUTC { get; init; }` added (verified at lines 112–113 of `MailBridgeRuntimeTestDoubles.cs`).
- All tests use `[TestClass]`/`[TestMethod]`, FluentAssertions, and no real I/O.
- Evidence artifact: `evidence/qa-gates/qa-ac3.md` — PASS.

**Checked off in issue.md**: Already marked `[x]`. Confirmed correct.

---

### AC-4
**Criterion**: Full toolchain passes (CSharpier → MSBuild analyzers → nullable build → dotnet test) with no new failures and repository-wide coverage >= 80%.

**Status**: PASS (with caveat — see note)

**Evidence**:

| Step | EXIT_CODE | Notes |
|---|---|---|
| CSharpier format + check | 0 | 94 files formatted/checked |
| dotnet build (analyzers) | 0 | 0 warnings, 0 errors |
| dotnet build (nullable) | 0 | 0 warnings, 0 errors |
| dotnet test | 0 | 287 passed, 0 failed, 3 skipped |

Coverage delta:
- Baseline: 94.1% (9619/10222 lines)
- Post-change: 94.2% (9730/10334 lines)
- Delta: +0.1% (no regression)
- `GetOptionalUtcDateTimeOffset` class-level coverage: 90.0% (at threshold)

**Caveat**: No machine-parseable coverage artifact (`artifacts/csharp/coverage.xml`) is present. The coverage numbers are accepted from the implementation agent's `qa-coverage-delta.md` and `qa-test.md` artifacts at face value. The policy audit records the missing artifact as a FAIL finding, but the reported numbers satisfy AC-4's >= 80% requirement.

**Checked off in issue.md**: Already marked `[x]`. Confirmed correct with the caveat above.

---

## Verification Notes Compliance

The issue's Verification Notes state:
> On any machine timezone, `GetOptionalUtcDateTimeOffset` called with `DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Unspecified)` must return a `DateTimeOffset` with `UtcDateTime == 2026-04-27T17:00:00Z` and `Offset == TimeSpan.Zero`, regardless of the local machine timezone.

This is directly tested in `GetOptionalUtcDateTimeOffset_UnspecifiedKind_returns_offset_with_zero_offset` and `ScanCalendarAsync_StartUTC_UnspecifiedKind_stores_correct_utc`. Both tests assert `UtcDateTime == new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc)` and `Offset == TimeSpan.Zero`. The tests are deterministic across machine timezones because `DateTimeKind.Unspecified` processing does not involve the local timezone offset.

Verification notes compliance: PASS.

---

## Out-of-Scope Committed Changes

The following changes are committed to this branch but are unrelated to Issue #45:

1. `scripts/Uninstall.ps1` — conditional record-deletion fix (from PR #51 — bug/uninstall-failure)
2. `tests/scripts/Uninstall.Tests.ps1` — test updates for above fix (from PR #51)
3. `docs/mailbridge-runbook.md` — runbook path improvements (from commits in PR #51 merge chain)

These appear on this branch because the worktree was initialized after PR #51 merged to main. They are not part of Issue #45's scope. The runbook change at line 199 contains a syntax error (backtick instead of closing single-quote in the `$versionNum` assignment), recorded as a defect in the policy audit and code review.

---

## Acceptance Criteria Status

- Source: `docs/features/active/2026-04-25-calendar-windows-wrong-45/issue.md`
- Total AC items: 4
- Checked off (delivered): 4
- Remaining (unchecked): 0

All four AC items were already marked `[x]` by the implementation agent and are confirmed correct by this review. No changes to issue.md AC checkbox state are required.

---

## Overall Verdict

**PARTIAL** — The implementation is functionally correct and all four AC items are satisfied by the working-tree code. However:

1. The C# implementation changes are not committed to the branch. Merging the branch as-is would not deliver the calendar UTC fix.
2. No `artifacts/csharp/coverage.xml` coverage artifact is present.
3. No `artifacts/pester/powershell-coverage.xml` coverage artifact is present.
4. `docs/mailbridge-runbook.md` line 199 contains a PowerShell syntax error (backtick vs single-quote).

Remediation is required before this branch is ready to merge. See `remediation-inputs.2026-04-25T17-00.md`.
