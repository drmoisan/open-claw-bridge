# Plan — Issue #55: Calendar Windows Wrong (UTC Double-Shift)

- Work Mode: full-bug
- Feature Folder: docs/features/active/2026-04-25-calendar-windows-wrong-45/
- Plan Path: docs/features/active/2026-04-25-calendar-windows-wrong-45/plan.md

---

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read policy file `CLAUDE.md` and record acknowledgement in `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/phase0-instructions-read.md`.
- [x] [P0-T2] Read policy file `.claude/rules/general-code-change.md` and append to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/phase0-instructions-read.md`.
- [x] [P0-T3] Read policy file `.claude/rules/general-unit-test.md` and append to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/phase0-instructions-read.md`.
- [x] [P0-T4] Read policy file `.claude/rules/csharp.md` and append to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/phase0-instructions-read.md`; set `Timestamp:`, `Policy Order:`, and explicit file list in that artifact.
- [x] [P0-T5] Run `dotnet tool run csharpier . --check` from repo root; write result (Timestamp, Command, EXIT_CODE, Output Summary) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/baseline-format.md`.
- [x] [P0-T6] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` from repo root; write result (Timestamp, Command, EXIT_CODE, Output Summary) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/baseline-lint.md`.
- [x] [P0-T7] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` from repo root; write result (Timestamp, Command, EXIT_CODE, Output Summary) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/baseline-nullable.md`.
- [x] [P0-T8] Run `dotnet test OpenClaw.MailBridge.sln --no-build -c Debug --collect:"Code Coverage"` from repo root; write result (Timestamp, Command, EXIT_CODE, Output Summary including numeric coverage headline) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/baseline/baseline-test.md`.

---

### Phase 1 — Implementation

- [x] [P1-T1] In `src/OpenClaw.MailBridge/OutlookComHelpers.cs`, add internal static method `GetOptionalUtcDateTimeOffset(object target, string memberName)` immediately after the closing brace of `GetOptionalDateTimeOffset` (after line 103). The method must use a `switch` expression with these arms:
  - `DateTimeOffset dto` → `dto.ToUniversalTime()`
  - `DateTime { Kind: DateTimeKind.Utc } dateTime` → `new DateTimeOffset(dateTime)`
  - `DateTime { Kind: DateTimeKind.Local } dateTime` → `new DateTimeOffset(dateTime.ToUniversalTime())`
  - `DateTime dateTime` (Unspecified — already UTC from Outlook) → `new DateTimeOffset(dateTime, TimeSpan.Zero)`
  - `_ when DateTimeOffset.TryParse(value?.ToString(), out var parsed)` → `parsed`
  - `_` → `null`
  - Wrap in the same `try { … } catch { return null; }` pattern used by `GetOptionalDateTimeOffset`. Include an XML `<summary>` doc comment stating that Unspecified kind is treated as already UTC.

- [x] [P1-T2] In `src/OpenClaw.MailBridge/OutlookScanner.cs`, update the `NormalizeEvent` method (lines 422–427): replace `OutlookComHelpers.GetOptionalDateTimeOffset(item, "StartUTC")` with `OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "StartUTC")` and replace `OutlookComHelpers.GetOptionalDateTimeOffset(item, "EndUTC")` with `OutlookComHelpers.GetOptionalUtcDateTimeOffset(item, "EndUTC")`. The fallback calls for `Start` and `End` must remain as `GetOptionalDateTimeOffset`.

- [x] [P1-T3] Create `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs` as a new `[TestClass]` named `OutlookComHelpersDateTimeKindTests` containing the following `[TestMethod]` tests, each following Arrange–Act–Assert with FluentAssertions:
  - `GetOptionalUtcDateTimeOffset_UnspecifiedKind_returns_offset_with_zero_offset`: pass a fake object exposing a `DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Unspecified)` property; assert `result.Value.UtcDateTime == new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc)` and `result.Value.Offset == TimeSpan.Zero`.
  - `GetOptionalUtcDateTimeOffset_UtcKind_returns_same_utc_value`: pass `DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc)`; assert same UTC value preserved and `Offset == TimeSpan.Zero`.
  - `GetOptionalUtcDateTimeOffset_LocalKind_converts_to_utc`: pass `DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Local)`; assert `result.Value.UtcDateTime == new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Local).ToUniversalTime()` and `result.Value.Offset == TimeSpan.Zero`.
  - `GetOptionalUtcDateTimeOffset_DateTimeOffset_returns_utc`: pass a `DateTimeOffset(2026, 4, 27, 13, 0, 0, TimeSpan.FromHours(-4))`; assert `result.Value.UtcDateTime == new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc)`.
  - `GetOptionalUtcDateTimeOffset_MissingMember_returns_null`: pass an object without the property; assert `result is null`.
  - `GetOptionalUtcDateTimeOffset_StringValue_parses_to_utc`: pass a string `"2026-04-27T17:00:00Z"`; assert `result` is non-null.
  - Use a private nested helper class (e.g., `FakeDateTimeHolder`) with a single property to back each test case, avoiding any real I/O or filesystem access.

- [x] [P1-T4] Create or extend a scanner-level integration test in `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs` as `[TestClass]` named `OutlookScannerCalendarUtcTests` containing:
  - `ScanCalendarAsync_StartUTC_UnspecifiedKind_stores_correct_utc`: arrange a `FakeAppointmentItem`-like object that exposes a `StartUTC` property returning `DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Unspecified)` and `EndUTC` returning `DateTime(2026, 4, 27, 18, 0, 0, DateTimeKind.Unspecified)`. Because `FakeAppointmentItem` does not currently have `StartUTC`/`EndUTC` properties, add `public DateTime? StartUTC { get; init; }` and `public DateTime? EndUTC { get; init; }` to the existing `FakeAppointmentItem` class in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`. Build an `OutlookScanner` via the same `BuildScanner` helper pattern used in `OutlookScannerResponseStatusTests`, run `ScanCalendarAsync`, and assert the stored `EventDto.StartUtc.UtcDateTime == new DateTime(2026, 4, 27, 17, 0, 0, DateTimeKind.Utc)` and `EventDto.StartUtc.Offset == TimeSpan.Zero`.

---

### Phase 2 — Toolchain and Acceptance Verification

- [x] [P2-T1] Run `dotnet tool run csharpier .` from repo root (auto-format). If any files are changed, restart the Phase 2 loop from this task. Write result (Timestamp, Command, EXIT_CODE, Output Summary) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-format.md`.
- [x] [P2-T2] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:EnableNETAnalyzers=true /p:EnforceCodeStyleInBuild=true` from repo root. If the build fails, fix the error and restart from P2-T1. Write result (Timestamp, Command, EXIT_CODE, Output Summary) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-lint.md`.
- [x] [P2-T3] Run `msbuild OpenClaw.MailBridge.sln /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:Nullable=enable /p:TreatWarningsAsErrors=true` from repo root. If the build fails, fix the error and restart from P2-T1. Write result (Timestamp, Command, EXIT_CODE, Output Summary) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-nullable.md`.
- [x] [P2-T4] Run `dotnet test OpenClaw.MailBridge.sln --no-build -c Debug --collect:"Code Coverage"` from repo root. If any test fails, fix the code and restart from P2-T1. Write result (Timestamp, Command, EXIT_CODE, Output Summary including numeric post-change coverage headline) to `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-test.md`.
- [x] [P2-T5] Verify coverage delta: confirm post-change repository-wide line coverage (from P2-T4 Output Summary) is >= 80% and that `OutlookComHelpers.GetOptionalUtcDateTimeOffset` and `OutlookScannerCalendarUtcTests` new code meets >= 90% coverage. Record baseline (from P0-T8) and post-change values with a pass/fail conclusion in `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-coverage-delta.md`.
- [x] [P2-T6] Verify AC-1: confirm `GetOptionalUtcDateTimeOffset` exists in `src/OpenClaw.MailBridge/OutlookComHelpers.cs` with all four `DateTime` kind branches and the `Unspecified` arm using `TimeSpan.Zero`. Record PASS/FAIL in `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-ac1.md`.
- [x] [P2-T7] Verify AC-2: confirm `NormalizeEvent` in `src/OpenClaw.MailBridge/OutlookScanner.cs` calls `GetOptionalUtcDateTimeOffset` for `StartUTC`/`EndUTC` and `GetOptionalDateTimeOffset` for `Start`/`End`. Record PASS/FAIL in `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-ac2.md`.
- [x] [P2-T8] Verify AC-3: confirm `tests/OpenClaw.MailBridge.Tests/OutlookComHelpersDateTimeKindTests.cs` contains tests for all three `DateTime` kind branches (Unspecified, Utc, Local) plus the DateTimeOffset case, and that `tests/OpenClaw.MailBridge.Tests/OutlookScannerCalendarUtcTests.cs` contains the scanner integration test with `Unspecified` kind input. Record PASS/FAIL in `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-ac3.md`.
- [x] [P2-T9] Verify AC-4: confirm the Phase 2 toolchain run in P2-T1 through P2-T4 all exited with EXIT_CODE 0 and no new failures compared to baseline. Record PASS/FAIL in `docs/features/active/2026-04-25-calendar-windows-wrong-45/evidence/qa-gates/qa-ac4.md`.
