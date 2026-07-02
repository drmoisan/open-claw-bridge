# sensitivity-redaction — Plan

- **Issue:** #18 (co-delivers #20)
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T08-36
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata)

## Required References

- Cross-language code change policy: `.claude/rules/general-code-change.md`
- Cross-language unit test policy: `.claude/rules/general-unit-test.md`
- C# standards: `.claude/rules/csharp.md` (note: MSTest + FluentAssertions is the established suite framework; follow the suite per `spec.md` Constraints)
- Architecture boundaries: `.claude/rules/architecture-boundaries.md`
- Quality tiers: `.claude/rules/quality-tiers.md`
- Authoritative requirements: `docs/features/active/sensitivity-redaction-18/spec.md` (19 AC items in Groups A/B/C + toolchain AC), mirrored in `docs/features/active/sensitivity-redaction-18/issue.md`

**All work must comply with these policies; do not duplicate their content here.**

## Scope Summary

Coordinated change in `OpenClaw.MailBridge` (no schema changes, no Contracts changes):

1. **Group A (#18):** normalization-time sensitivity redaction for `Sensitivity` 2/3 in `NormalizeMessage` (`src/OpenClaw.MailBridge/OutlookScanner.cs`) and `BuildEventDto` (`src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs`), with never-ingest ordering, bridge-id-only logging, and cache-write persistence. Redaction logic lives in a new partial-class file `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` because `OutlookScanner.cs` (465 lines) must not grow.
2. **Group B (#20):** safe-mode shaping suppression completion in `src/OpenClaw.MailBridge/ResponseShaper.cs` (`ToJson`/`CcJson`/`SenderEmailResolved`/`FromEmailAddress` on messages; `Organizer`/`Categories` on events; `ProtectedFieldsAvailable = false` on both).
3. **Group C (#18 x #20):** shapers stop mutating `IsRedacted` in both modes; redaction survives enhanced mode; boundary sensitivity values untouched. Deliberate assertion changes in `tests/OpenClaw.MailBridge.Tests/ResponseShaperTests.cs` (lines 25, 56) and `tests/OpenClaw.MailBridge.Tests/ResponseShaperEventBodyFullTests.cs` (lines 55, 81).

Toolchain commands (C#): `csharpier format .` / `csharpier check .` (global CSharpier 1.3.0) → `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable as errors) → `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.

Evidence root (canonical, non-overridable): `docs/features/active/sensitivity-redaction-18/evidence/<kind>/`. Raw coverage intermediates (Cobertura XML, TRX) may stage under `artifacts/csharp/`; the evidence summary artifacts live only under the canonical evidence root. `<ts>` denotes an ISO-8601 `yyyy-MM-ddTHH-mm` timestamp captured at execution time.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture & Policy Compliance

- [x] [P0-T1] Read repo policy files in order: `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`; write `docs/features/active/sensitivity-redaction-18/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read.
  - Acceptance: artifact exists with all three required fields and lists all six policy files.
- [x] [P0-T2] Read all five feature documents (`docs/features/active/sensitivity-redaction-18/spec.md`, `issue.md`, `user-story.md`, `github-issue-18.md`, `github-issue-20.md`) and append the list (with `Timestamp:`) to `docs/features/active/sensitivity-redaction-18/evidence/baseline/phase0-instructions-read.md`.
  - Acceptance: artifact lists all five feature documents; `spec.md` is noted as the authoritative AC source.
- [x] [P0-T3] Capture the formatting baseline: run `csharpier check .` from the repo root and write `docs/features/active/sensitivity-redaction-18/evidence/baseline/csharpier-check.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all four fields; `Output Summary:` states pass/fail and any unformatted file count.
- [x] [P0-T4] Capture the build/lint/type-check baseline: run `dotnet build OpenClaw.MailBridge.sln` and write `docs/features/active/sensitivity-redaction-18/evidence/baseline/dotnet-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (error/warning counts).
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0` expected on a clean baseline.
- [x] [P0-T5] Capture the test-and-coverage baseline: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and write `docs/features/active/sensitivity-redaction-18/evidence/baseline/dotnet-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including test pass/fail counts and **numeric baseline line and branch coverage percentages** parsed from the Cobertura report (raw report may stage under `artifacts/csharp/`).
  - Acceptance: artifact exists with all four fields; `Output Summary:` contains numeric line % and branch % values (placeholders such as `UNVERIFIED` are invalid).
- [x] [P0-T6] Record baseline line counts for the files in scope (`src/OpenClaw.MailBridge/OutlookScanner.cs` — expected 465, `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` — expected 117, `src/OpenClaw.MailBridge/ResponseShaper.cs` — expected 77, `tests/OpenClaw.MailBridge.Tests/ResponseShaperTests.cs`, `tests/OpenClaw.MailBridge.Tests/ResponseShaperEventBodyFullTests.cs`) in `docs/features/active/sensitivity-redaction-18/evidence/baseline/file-line-counts.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact lists a line count for each of the five files; the `OutlookScanner.cs` count is the not-to-exceed reference for P5-T6.

### Phase 1 — Pure Redaction Helpers (new partial file)

Sequencing note: the pure helpers are new API, so their unit tests cannot compile before the helpers exist; the phase is self-validating with creation followed immediately by tests. Scanner-level and shaper-level behavior changes (Phases 2–3) are test-first with `[expect-fail]` evidence.

- [x] [P1-T1] Create `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` as a partial-class file of `OutlookScanner` containing pure, COM-free, internally-visible redaction members: `IsSensitive(int? sensitivity)` returning `true` only for 2 and 3; a message redaction transform taking a `MessageDto` and returning the redacted `MessageDto` per the spec disposition table (`Subject = "Private message"`; `SenderName`/`SenderEmail`/`SenderEmailResolved`/`FromEmailAddress`/`ToJson`/`CcJson`/`BodyPreview` null; `IsRedacted = true`; `ProtectedFieldsAvailable = false`; all mechanical fields retained); an event redaction transform taking an `EventDto` and returning the redacted `EventDto` (`Subject = "Private appointment"`; `Location`/`Organizer`/`RequiredAttendeesJson`/`OptionalAttendeesJson`/`ResourcesJson`/`BodyPreview`/`BodyFull` null; `Categories = Array.Empty<string>()`; `IsRedacted = true`; `ProtectedFieldsAvailable = false`; all mechanical fields retained). File must be <= 500 lines and must not reference COM helpers or `object` COM items.
  - Acceptance: file compiles via `dotnet build OpenClaw.MailBridge.sln`; no member in the file accepts a COM `object` or calls `OutlookComHelpers`.
  - AC mapped: A1, A2, A3, A4 (transform level), C5 (`IsSensitive` boundary contract).
- [x] [P1-T2] Create `tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionTests.cs` (MSTest + FluentAssertions, AAA, deterministic, no temp files) covering: `IsSensitive` for 2 and 3 (`true`) and for 0, 1, `null`, -1, 4, 99 (`false`); full field disposition of the message transform including every retained mechanical field (`BridgeId`, `ItemKind`, `MessageClass`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `ConversationId`, `MeetingMessageType`); full field disposition of the event transform including retained fields (`BridgeId`, `GlobalAppointmentId`, `StartUtc`, `EndUtc`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `Sensitivity`, `SensitivityLabel`, `ResponseStatus`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, `LastModifiedDateTime`) and `Categories` being an empty (non-null) array.
  - Acceptance: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerRedactionTests"` exits 0 with all new tests passing; file <= 500 lines.
  - AC mapped: A1, A2, A3, A4, C5.
- [x] [P1-T3] Verify the Phase 1 increment cleanly passes the loop: run `csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, and the full `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings`; restart from formatting if any step fails or changes files.
  - Acceptance: all four commands exit 0 in one consecutive pass; no existing test regresses.

### Phase 2 — Never-Ingest Normalization Integration (#18, Group A)

- [x] [P2-T1] Create `tests/OpenClaw.MailBridge.Tests/SensitivityRedactionTestDoubles.cs` with access-recording COM item doubles modeled on the fakes in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`: a sensitive-mail-item double whose `Body`, `Recipients`, `Sender`, and SMTP-resolution members record (and optionally fail) any access, and a sensitive-appointment-item double whose `Body`, `Organizer`, recipient/attendee, `Location`, and `Categories` members record any access. File <= 500 lines; do not grow `MailBridgeRuntimeTestDoubles.cs`.
  - Acceptance: file compiles; each double exposes boolean/access-log members a test can assert against.
  - AC mapped: A5 (instrumentation prerequisite).
- [x] [P2-T2] [expect-fail] Create `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs` exercising the scanner through the existing `FakeComActiveObject`/`FakeScanStateRepository` scan pattern (see `tests/OpenClaw.MailBridge.Tests/OutlookScannerGraphFieldsTests.cs`): (a) a `Sensitivity=2` and a `Sensitivity=3` message produce fully redacted `MessageDto`s per the Group A disposition; (b) same for events including `Categories = []` and `BodyFull = null`; (c) protected COM members (body, recipient enumeration, sender SMTP resolution; event organizer/attendees/location/categories) are never accessed for sensitive items, asserted via the P2-T1 doubles; (d) boundary values 0, 1, `null`, -1, 4, 99 produce unredacted DTOs with `IsRedacted = false` and all original fields intact; (e) each redaction emits one Information-level log line containing the bridge id and none of the protected strings, asserted with an in-test capturing `ILogger<OutlookScanner>` double. Run the new tests before implementation and write the failing output to `docs/features/active/sensitivity-redaction-18/evidence/regression-testing/redaction-normalization-fail-before.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: fail-before artifact exists showing the new tests failing (non-zero exit) while compiling successfully; file <= 500 lines (split into a second test file if needed).
  - AC mapped: A1, A2, A3, A4, A5, A7, C5.
- [x] [P2-T3] Update `NormalizeMessage` in `src/OpenClaw.MailBridge/OutlookScanner.cs`: read `Sensitivity` (mechanical member) before any protected read; for sensitive items skip `ResponseShaper.ShapePreview`, the COM `Body` read, `ComMessageSource` recipient enumeration (`ToRecipients`/`CcRecipients`), and sender SMTP resolution (`SenderEmailResolved`/`FromEmailAddress`), constructing the DTO from mechanical members only and applying the Phase 1 redaction transform. Move construction logic into `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` as needed so `OutlookScanner.cs` does not exceed its baseline 465 lines (COM-reading sensitive-path normalization may live in the partial file; the Phase 1 transforms themselves stay pure).
  - Acceptance: `dotnet build OpenClaw.MailBridge.sln` exits 0; `OutlookScanner.cs` line count <= 465 (compare against P0-T6); message assertions in P2-T2 tests pass.
  - AC mapped: A1, A2, A5, C5.
- [x] [P2-T4] Update `BuildEventDto` in `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs`: read `Sensitivity` before `Body`, `Organizer`, `ReadAttendees`, `Location`, and `Categories`; for sensitive items branch to redacted event construction (mechanical members only, then the Phase 1 event transform), preserving `SensitivityLabel` via `EventSensitivityLabel.FromSensitivity` and the derived mechanical fields (`IsOrganizer`, `SeriesMasterId`, `ICalUId`, `LastModifiedDateTime`). File <= 500 lines.
  - Acceptance: `dotnet build OpenClaw.MailBridge.sln` exits 0; event assertions in P2-T2 tests pass.
  - AC mapped: A3, A4, A5, C5.
- [x] [P2-T5] Add exactly one `LogInformation` per redacted item (message and event paths) recording the bridge id only — never subject, sender, body, attendee, location, or category data — implemented as an instance member in `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` using the existing `_logger` field.
  - Acceptance: the P2-T2 log-capture tests pass; grep of the new log template strings confirms no protected-field placeholders.
  - AC mapped: A7.
- [x] [P2-T6] Create `tests/OpenClaw.MailBridge.Tests/CacheRepositorySensitivityRedactionTests.cs` following the in-memory SQLite pattern of `tests/OpenClaw.MailBridge.Tests/CacheRepositoryGraphFieldsTests.cs` (unique `Mode=Memory;Cache=Shared` data source, no temp files): scan a `Sensitivity=2` fake message and a `Sensitivity=3` fake event into a real `CacheRepository` via `UpsertMessageAsync`/`UpsertEventAsync`, read back via `GetMessageAsync`/`GetEventAsync` with no `ResponseShaper` call, and assert the stored rows are fully redacted (including `Categories` round-tripping as an empty array and `ProtectedFieldsAvailable = false`).
  - Acceptance: new tests pass; no `ResponseShaper` reference appears in the test's act/assert path; file <= 500 lines.
  - AC mapped: A6.
- [x] [P2-T7] Verify Phase 2 end state: rerun the P2-T2 test file (previously failing tests now pass) and the full loop (`csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings`), restarting from formatting on any failure or file change.
  - Acceptance: all commands exit 0 in one consecutive pass; the fail-before tests from P2-T2 are green.

### Phase 3 — Safe-Mode Suppression & IsRedacted Ownership (#20, Groups B/C)

- [x] [P3-T1] [expect-fail] Update the four deliberately-changing tests to the new semantics, recording the rationale (the conflation defect: `IsRedacted` becomes the exclusive sensitivity-redaction signal; `ProtectedFieldsAvailable = false` becomes the suppression signal — spec "Existing tests whose behavior deliberately changes" table) as a comment at each change site: (1) `tests/OpenClaw.MailBridge.Tests/ResponseShaperTests.cs:15-27` — rename `ShapeMessage_in_safe_mode_should_redact_sender_fields_and_clear_preview` to reflect suppression semantics, replace `IsRedacted.Should().BeTrue()` with a false-preserving assertion, add `ToJson`/`CcJson`/`SenderEmailResolved`/`FromEmailAddress` null and `ProtectedFieldsAvailable.Should().BeFalse()` assertions; (2) `ResponseShaperTests.cs:47-58` — same `IsRedacted` change, add `Organizer` null and `ProtectedFieldsAvailable` assertions; (3) `tests/OpenClaw.MailBridge.Tests/ResponseShaperEventBodyFullTests.cs:42-56` — rename `ShapeEvent_in_safe_mode_should_null_body_full_and_set_redacted`, invert the `IsRedacted` assertion; (4) `ResponseShaperEventBodyFullTests.cs:66-82` — same `IsRedacted` change. Update the `CreateMessage` helper in `ResponseShaperTests.cs` to populate `ToJson`/`CcJson`/`SenderEmailResolved`/`FromEmailAddress` so the null assertions are meaningful. Run both files and write the failing output to `docs/features/active/sensitivity-redaction-18/evidence/regression-testing/shaper-assertion-change-fail-before.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: fail-before artifact exists showing the updated tests failing against the current shaper; each changed assertion carries a rationale comment; both files <= 500 lines.
  - AC mapped: B1, B3, C3 (assertion-change half).
- [x] [P3-T2] [expect-fail] Create `tests/OpenClaw.MailBridge.Tests/ResponseShaperSafeModeSuppressionTests.cs` covering: B1 — safe-mode `ShapeMessage` nulls `ToJson`, `CcJson`, `SenderEmailResolved`, `FromEmailAddress` plus the existing `BodyPreview`/`SenderName`/`SenderEmail`, and sets `ProtectedFieldsAvailable = false`; B2 — safe-mode `ShapeMessage` retains `BridgeId`, `ItemKind`, `Subject`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `MessageClass`, `ConversationId`, `MeetingMessageType`; B3 — safe-mode `ShapeEvent` nulls `Organizer` plus the existing five suppressed fields, sets `Categories` to an empty array and `ProtectedFieldsAvailable = false`; B4 — safe-mode `ShapeEvent` retains `Location` and all mechanical fields (full spec list); B5 — enhanced mode nulls nothing, does not force `ProtectedFieldsAvailable = false`, still sanitizes/truncates `BodyPreview` and returns `BodyFull` verbatim; B6 — a `MessageDto`/`EventDto` with already-null protected fields shapes without error in both modes. Include failing output in the same run as P3-T3's fail-before capture or a separate `docs/features/active/sensitivity-redaction-18/evidence/regression-testing/shaper-suppression-fail-before.<ts>.md`.
  - Acceptance: file compiles; fail-before evidence exists for the B1/B3 assertions (B2/B4/B6 may already pass — note which in `Output Summary:`); file <= 500 lines.
  - AC mapped: B1, B2, B3, B4, B5, B6.
- [x] [P3-T3] [expect-fail] Create `tests/OpenClaw.MailBridge.Tests/ResponseShaperCompositionInvariantTests.cs` covering: C1 — a redacted `MessageDto`/`EventDto` (built with the Phase 1 transforms) passed through **enhanced**-mode shaping keeps `IsRedacted = true` and all redacted fields null/empty; C2 — the same redacted DTOs through **safe**-mode shaping keep `IsRedacted = true` and do not throw; C3 — neither `ShapeMessage` nor `ShapeEvent` mutates `IsRedacted` in either mode (safe with input `false` stays `false`; enhanced with input `true` stays `true`); C4 — `ProtectedFieldsAvailable = false` holds on both paths (redaction-written value survives enhanced shaping; safe mode forces it on unredacted DTOs). Write failing output to `docs/features/active/sensitivity-redaction-18/evidence/regression-testing/composition-invariants-fail-before.<ts>.md` with the four schema fields.
  - Acceptance: fail-before artifact exists showing C1/C3 failing against the current shaper (which forces `IsRedacted = false` in enhanced mode); file <= 500 lines.
  - AC mapped: C1, C2, C3, C4.
- [x] [P3-T4] Update `src/OpenClaw.MailBridge/ResponseShaper.cs`: in `ShapeMessage`, extend the safe branch with `ToJson = null`, `CcJson = null`, `SenderEmailResolved = null`, `FromEmailAddress = null`, `ProtectedFieldsAvailable = false`, and remove `IsRedacted = true`; remove `IsRedacted = false` from the enhanced branch. In `ShapeEvent`, extend the safe branch with `Organizer = null`, `Categories = Array.Empty<string>()`, `ProtectedFieldsAvailable = false`, and remove `IsRedacted = true`; remove `IsRedacted = false` from the enhanced branch; retain `Location`. Update the lines 43-50 comment block to the new semantics (`IsRedacted` = sensitivity-only signal; `ProtectedFieldsAvailable = false` = suppression signal). File <= 500 lines.
  - Acceptance: `dotnet build OpenClaw.MailBridge.sln` exits 0; all P3-T1/T2/T3 tests pass; no `IsRedacted` assignment remains anywhere in `ResponseShaper.cs` (grep-verifiable).
  - AC mapped: B1, B2, B3, B4, B5, B6, C1, C2, C3, C4.
- [x] [P3-T5] Verify unmodified regression guards still pass: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~MailBridgeTests|FullyQualifiedName~ResponseShaper"` and confirm `Safe_mode_message_shaping_should_suppress_body_preview_sender_name_and_sender_email` (`tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs:38`) passes without edits, and the enhanced-mode tests at `ResponseShaperTests.cs:30-45,60-70` and `ResponseShaperEventBodyFullTests.cs:85-99,101-127` pass under the preserve semantics.
  - Acceptance: filtered test run exits 0; `MailBridgeTests.cs` shows no diff in `git diff --name-only`.
  - AC mapped: B1, B3 (no-regression clauses), C3.
- [x] [P3-T6] Verify Phase 3 end state with the full loop: `csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings`; restart from formatting on any failure or file change.
  - Acceptance: all commands exit 0 in one consecutive pass; all fail-before tests from P3-T1/T2/T3 are green.

### Phase 4 — Documentation & Consistency Checks

- [x] [P4-T1] Search `README.md` and `docs/` (excluding `docs/features/`) for stale statements about `is_redacted`, `protected_fields_available`, or safe-mode field suppression (`rg -i "is_redacted|protected_fields_available|IsRedacted" README.md docs/ --glob '!docs/features/**'`); update any stale statement to the new semantics and record the search output plus the update-or-no-change decision in `docs/features/active/sensitivity-redaction-18/evidence/other/docs-review.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists; every hit is either updated or explicitly recorded as still-accurate.
- [x] [P4-T2] Write the change-description draft (source for the PR body) to `docs/features/active/sensitivity-redaction-18/evidence/other/change-description.<ts>.md` enumerating: (1) the two deliberate breaking behavioral changes (`is_redacted` no longer signals safe mode — use `protected_fields_available: false`; safe mode now nulls `to_json`/`cc_json`/`sender_email_resolved`/`from_email_address`/`organizer` and empties `categories`); (2) the deployment note that previously cached unredacted Private/Confidential rows persist until re-scan and a cache flush or forced re-scan is recommended; (3) the redaction log line addition (bridge id only).
  - Acceptance: artifact contains all three items with a `Timestamp:` header.
- [x] [P4-T3] Verify no schema or contracts change occurred: run `git diff --name-only` and confirm no file under `src/OpenClaw.MailBridge.Contracts/` and no `CacheRepository.Schema*.cs` file is modified; record the command output in the P4-T1 artifact (`docs-review.<ts>.md`) or a dedicated section of `docs/features/active/sensitivity-redaction-18/evidence/other/docs-review.<ts>.md`.
  - Acceptance: recorded diff file list contains no Contracts or schema files (spec "No schema changes" invariant).

### Phase 5 — Final QA Loop & Coverage Comparison

Loop rule (non-skippable): if any of P5-T1 through P5-T3 fails or modifies files, remediate and restart from P5-T1; the recorded artifacts must all come from the final consecutive clean pass. `EXIT_CODE: SKIPPED` is invalid for every task in this phase.

- [x] [P5-T1] Run `csharpier format .` then `csharpier check .` from the repo root; write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` for the check command.
  - Acceptance: artifact exists; `EXIT_CODE: 0`; `Output Summary:` confirms zero unformatted files.
- [x] [P5-T2] Run `dotnet build OpenClaw.MailBridge.sln` (analyzers, nullable-as-errors, architecture assertions compiled in); write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-build.<ts>.md` with the four schema fields.
  - Acceptance: artifact exists; `EXIT_CODE: 0`; zero warnings/errors reported in `Output Summary:`.
- [x] [P5-T3] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-test-coverage.<ts>.md` with the four schema fields, including test counts and **numeric post-change line and branch coverage percentages** (raw Cobertura/TRX may stage under `artifacts/csharp/`).
  - Acceptance: artifact exists; `EXIT_CODE: 0`; numeric line % and branch % recorded (placeholders invalid).
- [x] [P5-T4] Confirm the final pass was a single consecutive clean pass: verify the P5-T1/T2/T3 artifacts carry `EXIT_CODE: 0` from the same final iteration and that no file changed between P5-T1's format run and P5-T3's test run (`git status --porcelain` stable across the pass); record the confirmation in `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-single-pass.<ts>.md`.
  - Acceptance: artifact exists and cites the three sibling artifacts by filename with matching iteration timestamps.
- [x] [P5-T5] Produce the coverage comparison: compare P0-T5 baseline against P5-T3 post-change values and verify (a) line coverage >= 85%, (b) branch coverage >= 75%, (c) no regression versus baseline, (d) every changed production file (`OutlookScanner.cs`, `OutlookScanner.Redaction.cs`, `OutlookScanner.GraphFields.cs`, `ResponseShaper.cs`, enumerated via `git diff --name-only`) has its changed lines covered per the Cobertura report; write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-comparison.<ts>.md` reporting baseline coverage, post-change coverage, and changed-file/new-code coverage.
  - Acceptance: artifact contains all three numeric groups and an explicit PASS/FAIL verdict per threshold; if any required value is unavailable, the outcome is remediation-required, not PASS.
- [x] [P5-T6] Verify file-size caps: confirm every touched production and test file is <= 500 lines and `src/OpenClaw.MailBridge/OutlookScanner.cs` is <= 465 lines (its P0-T6 baseline); write counts to `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/file-size-check.<ts>.md` with the four schema fields.
  - Acceptance: artifact lists each touched file with its line count; all within limits.
- [x] [P5-T7] Produce the AC traceability check: for each of the 19 AC items (A1–A7, B1–B6, C1–C5, plus the toolchain/coverage AC) verify at least one passing named test or evidence artifact exists per the Traceability table below; write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/ac-traceability.<ts>.md` mapping each AC to its test class/method or artifact path with a per-item PASS/FAIL.
  - Acceptance: artifact covers all 19 AC items; any FAIL blocks completion (verdict must be remediation-required, never PASS-with-gaps).

## Test Plan

- **Unit (pure transforms):** `tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionTests.cs` — `IsSensitive` boundaries, message/event redaction field dispositions (Phase 1).
- **Unit (scanner integration, fake COM):** `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs` + `SensitivityRedactionTestDoubles.cs` — redacted normalization, never-ingest access assertions, boundary values, log-capture assertions (Phase 2). No live COM, no temp files, deterministic fixed clock (`Func<DateTimeOffset>` seam).
- **Integration (cache round-trip, in-memory SQLite):** `tests/OpenClaw.MailBridge.Tests/CacheRepositorySensitivityRedactionTests.cs` — redaction persisted at cache-write, read back without shaping (Phase 2).
- **Unit (shaper):** updated `ResponseShaperTests.cs` / `ResponseShaperEventBodyFullTests.cs` (deliberate assertion changes with recorded rationale), new `ResponseShaperSafeModeSuppressionTests.cs` (Group B) and `ResponseShaperCompositionInvariantTests.cs` (Group C) (Phase 3).
- **Regression guard:** `MailBridgeTests.cs:38` safe-mode suppression test must pass unmodified.
- **Coverage evidence:** baseline `docs/features/active/sensitivity-redaction-18/evidence/baseline/dotnet-test-coverage.<ts>.md`; post-change `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-test-coverage.<ts>.md`; comparison `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-comparison.<ts>.md`. Raw reports stage under `artifacts/csharp/` (non-evidence intermediates only).

## AC Traceability

| AC | Spec item | Delivering tasks | Verifying tests/artifacts |
|---|---|---|---|
| A1 | Redacted message field set | P1-T1, P2-T3 | OutlookScannerRedactionTests, OutlookScannerSensitivityNormalizationTests |
| A2 | Message mechanical retention | P1-T1, P2-T3 | OutlookScannerRedactionTests, OutlookScannerSensitivityNormalizationTests |
| A3 | Redacted event field set | P1-T1, P2-T4 | OutlookScannerRedactionTests, OutlookScannerSensitivityNormalizationTests |
| A4 | Event mechanical retention | P1-T1, P2-T4 | OutlookScannerRedactionTests, OutlookScannerSensitivityNormalizationTests |
| A5 | Never-ingest ordering | P2-T1, P2-T3, P2-T4 | OutlookScannerSensitivityNormalizationTests (access-recording doubles) |
| A6 | Cache-write redaction round-trip | P2-T6 | CacheRepositorySensitivityRedactionTests |
| A7 | Bridge-id-only redaction logging | P2-T5 | OutlookScannerSensitivityNormalizationTests (capturing logger) |
| B1 | ShapeMessage safe-mode suppression | P3-T1, P3-T2, P3-T4 | ResponseShaperTests (updated), ResponseShaperSafeModeSuppressionTests |
| B2 | ShapeMessage safe-mode retention | P3-T2, P3-T4 | ResponseShaperSafeModeSuppressionTests |
| B3 | ShapeEvent safe-mode suppression | P3-T1, P3-T2, P3-T4 | ResponseShaperEventBodyFullTests (updated), ResponseShaperSafeModeSuppressionTests |
| B4 | ShapeEvent safe-mode retention (incl. Location) | P3-T2, P3-T4 | ResponseShaperSafeModeSuppressionTests |
| B5 | Enhanced-mode pass-through | P3-T2, P3-T4 | ResponseShaperSafeModeSuppressionTests, ResponseShaperEventBodyFullTests |
| B6 | Already-null fields shape without error | P3-T2, P3-T4 | ResponseShaperSafeModeSuppressionTests |
| C1 | Redaction survives enhanced mode | P3-T3, P3-T4 | ResponseShaperCompositionInvariantTests |
| C2 | Redacted DTO through safe mode keeps IsRedacted | P3-T3, P3-T4 | ResponseShaperCompositionInvariantTests |
| C3 | Shapers never mutate IsRedacted | P3-T1, P3-T3, P3-T4, P3-T5 | ResponseShaperCompositionInvariantTests, updated shaper tests |
| C4 | ProtectedFieldsAvailable=false on both paths | P3-T3, P3-T4 | ResponseShaperCompositionInvariantTests |
| C5 | Boundary sensitivity values untouched | P1-T1, P1-T2, P2-T2 | OutlookScannerRedactionTests, OutlookScannerSensitivityNormalizationTests |
| Toolchain/coverage | Full pass; line >= 85%, branch >= 75%, changed lines covered | P5-T1..P5-T7 | qa-gates artifacts (final-format, final-build, final-test-coverage, coverage-comparison, file-size-check, ac-traceability) |

## Open Questions / Notes

- Sequencing rationale: Phase 1 helper tests follow helper creation because the API does not exist to compile against; the behavior-changing Phases 2–3 are test-first with `[expect-fail]` fail-before evidence under `docs/features/active/sensitivity-redaction-18/evidence/regression-testing/`.
- `OutlookScanner.cs` must not exceed its 465-line baseline; the plan authorizes relocating sensitive-path construction into `OutlookScanner.Redaction.cs` to hold that line (P2-T3, verified in P5-T6). The Phase 1 DTO transforms remain pure and COM-free regardless.
- `Location` retention in safe mode is a decided behavior (spec section B): safe mode retains `Location`; only sensitivity redaction removes it. B4 tests pin this.
- Test framework is MSTest + FluentAssertions per the existing suite; `.claude/rules/csharp.md` names xUnit, but the spec directs following the established suite. Do not introduce xUnit.
- No feature flag; rollback path is revert. No dependency changes permitted.
- Evidence location invariant: all evidence under `docs/features/active/sensitivity-redaction-18/evidence/<kind>/`; `artifacts/csharp/` is staging for raw coverage intermediates only, never for evidence summaries.
