# mailbridge-event-attendees-json — Plan

- **Issue:** #71
- **Parent (optional):** Track M (MailBridge DTO/scanner): #72 -> #71 -> #73
- **Owner:** drmoisan
- **Last Updated:** 2026-06-13T10-31
- **Status:** Draft
- **Version:** 0.2
- **Work Mode:** full-feature (no `Work Mode` marker present in `issue.md`; resolves to full-feature per mode-source-precedence fail-closed rule, consistent with the caller directive)

## Required References

Policy compliance is governed by the auto-loaded `.claude/rules/` files. This plan does not duplicate their content. The authoritative policy set for this feature is:

- `CLAUDE.md` (standing instructions)
- `.claude/rules/general-code-change.md` (cross-language code change policy; seven-stage toolchain; 500-line file cap)
- `.claude/rules/general-unit-test.md` (cross-language unit test policy; determinism; no temp files)
- `.claude/rules/csharp.md` (C# toolchain: CSharpier -> SDK analyzers -> nullable -> architecture -> MSTest + coverage)
- `.claude/rules/architecture-boundaries.md` (COM confinement to `OpenClaw.MailBridge`; deterministic COM release)
- `.claude/rules/quality-tiers.md` (uniform coverage: line >= 85%, branch >= 75%; T2/T3 surface in scope)

**All work must comply with these policies; do not duplicate their content here.**

## Requirement Sources (Authoritative Acceptance Criteria)

- `docs/features/active/mailbridge-event-attendees-json-71/spec.md` — Definition of Done (lines 109–116) and Behavior/Constraints sections.
- `docs/features/active/mailbridge-event-attendees-json-71/user-story.md` — Acceptance Criteria (lines 42–55).

### Acceptance Criteria Identifiers

User-story Acceptance Criteria (US-AC):

- **US-AC1** — A scan of a meeting with known attendees returns non-null `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson` (as applicable) with correct names and emails in enhanced mode.
- **US-AC2** — Each populated field is a JSON array of `{"name","email"}` objects matching the Graph `emailAddress` shape (lowercase `name`/`email` keys, collection order preserved).
- **US-AC3** — A unit test asserts per-type classification: `Type 1` -> required, `Type 2` -> optional, `Type 3` -> resource; recipients with other `Type` values are excluded.
- **US-AC4** — Safe mode (`BridgeSettings.Mode == "safe"`) nulls all three attendee JSON fields via `ResponseShaper.ShapeEvent`, matching the existing redaction of `SenderName`/`SenderEmail`, asserted by a unit test.
- **US-AC5** — A recipient missing a name or resolvable email emits an empty string for the missing value with both keys still present, covered by a unit test.
- **US-AC6** — No `EventDto` contract shape change; line and branch coverage thresholds hold (line >= 85%, branch >= 75%) with no regression on changed lines.

Spec Definition-of-Done (SP-DoD), spec lines 109–116:

- **SP-DoD1** — Acceptance criteria documented and mapped to tests or demos.
- **SP-DoD2** — Behavior matches acceptance criteria in all documented environments.
- **SP-DoD3** — Tests updated/added (unit/integration as applicable).
- **SP-DoD4** — Edge cases and error handling covered by tests.
- **SP-DoD5** — Docs updated if applicable.
- **SP-DoD6** — Telemetry/logging added or updated (if applicable).
- **SP-DoD7** — Toolchain pass completed (format -> lint -> type-check -> test).

Spec Behavior/Constraints supplementary criteria:

- **SP-B1** — Empty-collection representation: a type with no recipients yields `[]` (not null); null is reserved for safe-mode redaction / unread state (spec lines 36–39, 88–90).
- **SP-B2** — Out-of-range `Type` values are ignored (spec line 42).
- **SP-B3** — Per-recipient COM read failure must not abort the event scan (fail-soft per recipient; spec lines 43–47).
- **SP-B4** — All COM objects obtained while enumerating `Recipients` (the collection and each `Recipient`) are released deterministically (spec lines 45–47; architecture-boundaries COM confinement).
- **SP-B5** — `ProtectedFieldsAvailable` decision: attendee readability must not weaken the existing body-derived signal (spec lines 83–85).

## Verified Current State (confirmed by reading)

- `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` — `BuildEventDto` hardcodes the three attendee JSON fields to `null` at positional arguments on lines 44 (RequiredAttendeesJson), 45 (OptionalAttendeesJson), 46 (ResourcesJson). File is 104 lines.
- `src/OpenClaw.MailBridge/OutlookScanner.cs` — 485 lines. The `GetStoreId` enumerate-then-release idiom (lines 463–484) uses `try/finally` with `_com.ReleaseAll(store, parent)`. The `_com` field is a `ComActiveObject` (line 23).
- `src/OpenClaw.MailBridge/OutlookComHelpers.cs` — 133 lines. Provides `GetOptionalString`, `GetOptionalInt`, `GetOptionalMemberValue`, `GetMemberValue`, `InvokeMember`, all internal static, fail-soft (catch -> null/default).
- `src/OpenClaw.MailBridge/ComActiveObject.cs` — `ReleaseAll(params object?[])` releases in reverse acquisition order (lines 118–124).
- `src/OpenClaw.MailBridge/ResponseShaper.cs` — `ShapeEvent` safe-mode branch (lines 53–58) currently nulls `BodyPreview` and `BodyFull` and sets `IsRedacted = true`. It does NOT null the three attendee JSON fields. `ShapeMessage` safe-mode branch (lines 25–31) is the parity reference: it nulls `SenderName`/`SenderEmail`.
- `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` — `EventDto` (lines 94–122); the three fields are `string?` at lines 106–108. NO contract shape change is permitted.
- `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs` — `FakeAppointmentItem` (record, lines 115–138) is read by reflection in `BuildEventDto`; `FakeComActiveObject` (lines 7–31) subclasses `ComActiveObject`. A `Recipients` analog must be added to `FakeAppointmentItem` so the helper can enumerate without live COM.
- `tests/OpenClaw.MailBridge.Tests/OutlookScannerGraphFieldsTests.cs` — established scanner-population test pattern (scan -> single event assertion).
- `tests/OpenClaw.MailBridge.Tests/ResponseShaperEventBodyFullTests.cs` — established `ShapeEvent` redaction test pattern.
- `mailbridge.runsettings` — present at repo root; coverage collected via `coverlet.collector`.

## Design Decisions Encoded in This Plan

1. **Recipient enumeration + grouping helper** is implemented as a new partial-class file `src/OpenClaw.MailBridge/OutlookScanner.Attendees.cs` (rather than growing `OutlookScanner.GraphFields.cs` to risk, or `OutlookScanner.cs` already at 485 lines). The helper performs a single pass over the COM `Recipients` collection, classifies each recipient by `Type` (1=required, 2=optional, 3=resource), and releases every COM wrapper (the collection and each `Recipient`, plus any `AddressEntry` obtained) deterministically via `_com.ReleaseAll` in a `try/finally`, following the `GetStoreId` idiom.
2. **Pure/COM separation.** The COM-reading method extracts each recipient into a small immutable in-memory record (display name + email + classified type) and delegates JSON shaping to a pure static function that accepts three ordered lists and returns three JSON strings. The pure function is unit-testable without COM.
3. **Carrier.** The COM method returns a small immutable carrier (e.g., a `readonly record struct` holding `RequiredJson`, `OptionalJson`, `ResourcesJson` strings) consumed by `BuildEventDto`, which substitutes its three results for the three `null` literals at lines 44–46.
4. **Email source.** Email is resolved from the simplest reliable SMTP source available on the COM `Recipient` surface, attempted in order: `Recipient.Address`, then `Recipient.AddressEntry` resolution when `Address` is unavailable. When no value is resolvable, emit empty string `""`. Both `name` and `email` keys are always present in every object (US-AC5, SP behavior).
5. **Empty-collection representation (SP-B1, resolves spec open question).** A type with zero recipients serializes to the empty array string `"[]"`, NOT null. Null is reserved for safe-mode redaction and unread/unpopulated state, so a consumer can distinguish "no attendees of this type" (`"[]"`) from "redacted" (`null`).
6. **Safe-mode parity (US-AC4, SP security).** `ResponseShaper.ShapeEvent` safe-mode branch additionally nulls `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson`, matching the message-path redaction of `SenderName`/`SenderEmail`.
7. **`ProtectedFieldsAvailable` decision (SP-B5).** The existing `ProtectedFieldsAvailable` signal remains derived solely from body availability (`!string.IsNullOrWhiteSpace(body)` at line 48). Attendee readability does NOT contribute to or gate that flag. Rationale: weakening or broadening the body-derived signal would change existing #72 behavior outside this issue's scope; attendee PII protection is enforced by safe-mode redaction (decision 6), which is the established mechanism. This decision is recorded in the helper/`BuildEventDto` XML doc and verified by a non-regression assertion.
8. **Serialization.** `System.Text.Json` with explicit lowercase property names `name` and `email` and a fixed (deterministic) property order; no culture-dependent formatting. Serialization options are constructed once (static) to avoid per-call allocation and to guarantee determinism.

## Constraints

- Languages in scope: **C# only**. Tiers: T2 (MailBridge managed surface) / T3 (Outlook-COM-confined surface).
- All Outlook COM stays in `OpenClaw.MailBridge`; release COM deterministically (architecture-boundaries).
- 500-line file cap for every production and test file.
- Tests: MSTest + Moq + FluentAssertions under `tests/OpenClaw.MailBridge.Tests/`; no temp files; no live COM; deterministic clock seam already present (`() => FixedNow`).
- Coverage gates: line >= 85%, branch >= 75%; no regression on changed lines.

## Evidence Location Invariant

All evidence artifacts produced by executing this plan MUST be written under the canonical scheme `docs/features/active/mailbridge-event-attendees-json-71/evidence/<kind>/`. Baseline evidence -> `evidence/baseline/`; QA-gate evidence -> `evidence/qa-gates/`; regression-testing evidence -> `evidence/regression-testing/`. Non-canonical paths such as `artifacts/baselines/`, `artifacts/qa/`, or `artifacts/coverage/` are forbidden and will be rejected by the `enforce-evidence-locations.ps1` PreToolUse hook. If a caller supplies a non-canonical path, substitute the canonical path and record `EVIDENCE_LOCATION_OVERRIDE_REJECTED: <supplied> replaced with <canonical>`.

Timestamp format for all evidence artifacts: `yyyy-MM-ddTHH-mm` (ISO-8601). Each command-step artifact MUST include `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`. Baseline and final-QC test-step artifacts MUST record numeric coverage values (line %, branch %, and changed-code %), not placeholders.

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture & Policy Read

- [x] [P0-T1] Read the policy files in the required order (`CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`) and record a Phase 0 policy-read evidence artifact.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/baseline/phase0-instructions-read.<timestamp>.md` exists and contains `Timestamp:`, `Policy Order:`, and the explicit list of files read.
- [x] [P0-T2] Capture the baseline CSharpier formatting state by running `dotnet csharpier check .` (or `csharpier check .`) from repo root.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/baseline/baseline-format.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (pass/fail and any files needing formatting).
- [x] [P0-T3] Capture the baseline build/analyzer state by running `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/baseline/baseline-build.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` (warning/error counts).
- [x] [P0-T4] Capture the baseline nullable/type-check state by running `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/baseline/baseline-nullable.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:`.
- [x] [P0-T5] Capture the baseline test + coverage state by running `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/baseline/baseline-test.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` including numeric baseline line coverage % and branch coverage % for the MailBridge solution (the pre-change reference for the no-regression check in [P4-T6]).

### Phase 1 — Pure JSON Shaping (COM-free, test-first)

- [x] [P1-T1] Create the pure attendee model and JSON-shaping function in a new file `src/OpenClaw.MailBridge/OutlookScanner.Attendees.cs`: define an internal immutable record carrier (`AttendeeJsonSet` with `RequiredJson`, `OptionalJson`, `ResourcesJson`) and a pure static method that accepts three ordered `IReadOnlyList<(string Name, string Email)>` inputs and serializes each to a JSON array of `{"name","email"}` objects using a single static `JsonSerializerOptions` with lowercase `name`/`email` keys and deterministic property order.
  - Acceptance: file compiles; the pure method has no COM dependency; an empty input list serializes to `"[]"`; file is under 500 lines. (US-AC2, SP-B1, design decisions 2/5/8)
- [x] [P1-T2] Add a unit test file `tests/OpenClaw.MailBridge.Tests/OutlookScannerAttendeesShapeTests.cs` asserting the pure shaping function emits lowercase `name`/`email` keys, preserves input order, and matches the Graph `emailAddress` shape for a multi-attendee list.
  - Acceptance: test passes; assertion verifies exact JSON string for a two-element list. (US-AC2)
- [x] [P1-T3] Add a unit test to `OutlookScannerAttendeesShapeTests.cs` asserting an empty input list serializes to `"[]"` (not null) for each of the three groups.
  - Acceptance: test passes. (US-AC2, SP-B1)
- [x] [P1-T4] Add a unit test to `OutlookScannerAttendeesShapeTests.cs` asserting a recipient with a missing name emits `"name":""` with the `email` key present, and a recipient with a missing email emits `"email":""` with the `name` key present.
  - Acceptance: test passes; both keys always present. (US-AC5, SP behavior)

### Phase 2 — COM Recipient Enumeration & BuildEventDto Wiring

- [x] [P2-T1] Implement a private instance method on the `OutlookScanner` partial in `src/OpenClaw.MailBridge/OutlookScanner.Attendees.cs` that reads the COM `Recipients` collection from `item`, enumerates it in a single pass (`Count` + 1-based `Item(index)` via `OutlookComHelpers`), reads each recipient's `Type`, `Name`, and email (from `Address`, falling back to `AddressEntry` resolution), classifies into required/optional/resource lists by `Type` (1/2/3), and ignores out-of-range `Type` values. Use `OutlookComHelpers.GetOptional*` for fail-soft per-recipient reads.
  - Acceptance: method compiles; per-recipient read failures do not throw (fail-soft); out-of-range `Type` values are excluded. (US-AC1, US-AC3, SP-B2, SP-B3)
- [x] [P2-T2] In the same method, release every COM wrapper obtained during enumeration — the `Recipients` collection, each `Recipient`, and any `AddressEntry` — deterministically in a `try/finally` using `_com.ReleaseAll`, following the `GetStoreId` idiom (`OutlookScanner.cs` lines 463–484).
  - Acceptance: every acquired COM object is released in the `finally`; no RCW accumulation path remains. (SP-B4, architecture-boundaries COM confinement)
- [x] [P2-T3] Have the COM method delegate to the Phase 1 pure shaping function and return the `AttendeeJsonSet` carrier; a `Recipients` collection that is null or empty yields `"[]"` for all three fields.
  - Acceptance: method returns three non-null JSON strings; null/empty `Recipients` yields three `"[]"` values. (US-AC1, SP-B1)
- [x] [P2-T4] Update `BuildEventDto` in `src/OpenClaw.MailBridge/OutlookScanner.GraphFields.cs` to call the COM enumeration method once and replace the three `null` positional arguments at lines 44–46 with `RequiredJson`, `OptionalJson`, `ResourcesJson` from the carrier. Do not change argument count or order.
  - Acceptance: `EventDto` construction passes the three attendee JSON strings; positional argument count/order unchanged; solution builds. (US-AC1, US-AC6)
- [x] [P2-T5] Confirm and document (in the method/`BuildEventDto` XML doc) that `ProtectedFieldsAvailable` remains derived solely from body availability (`!string.IsNullOrWhiteSpace(body)`, line 48) and is not altered by attendee readability.
  - Acceptance: line 48 logic is unchanged; XML doc states the decision. (SP-B5, design decision 7)
- [x] [P2-T6] Extend `FakeAppointmentItem` in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs` with a `Recipients` analog that supports reflection-based COM-style enumeration (`Count` and 1-based `Item(index)`), where each recipient exposes `Type`, `Name`, and `Address`/`AddressEntry`, so the COM enumeration method can be exercised without live COM.
  - Acceptance: the double is reflection-readable by `OutlookComHelpers`; no live COM; no temp files; file remains under 500 lines (split into a sibling doubles file if the cap is approached). (csharp testing standards)
- [x] [P2-T7] Add a unit test to `tests/OpenClaw.MailBridge.Tests/OutlookScannerGraphFieldsTests.cs` (or a new sibling `OutlookScannerAttendeesTests.cs` if the file nears 500 lines) asserting that a scanned meeting with required, optional, and resource recipients yields non-null `RequiredAttendeesJson`/`OptionalAttendeesJson`/`ResourcesJson` with correct names and emails in enhanced mode.
  - Acceptance: test passes; emails and names match the input recipients. (US-AC1)
- [x] [P2-T8] Add a unit test asserting per-type classification: a recipient with `Type 1` appears only in required, `Type 2` only in optional, `Type 3` only in resource, and a recipient with an out-of-range `Type` (e.g., 0 or 4) appears in none of the three fields.
  - Acceptance: test passes; out-of-range recipient is excluded from all three. (US-AC3, SP-B2)
- [x] [P2-T9] Add a unit test asserting a recipient with a missing name and a recipient with a missing email each produce objects with both `name` and `email` keys present and the missing value as `""`, via the scanner path.
  - Acceptance: test passes. (US-AC5)
- [x] [P2-T10] Add a unit test asserting a meeting with no recipients of a given type yields `"[]"` (not null) for that field in enhanced mode, and a meeting with an empty/absent `Recipients` collection yields `"[]"` for all three.
  - Acceptance: test passes; distinguishes `"[]"` (no attendees) from `null` (redacted). (SP-B1)

### Phase 3 — Safe-Mode Redaction Parity

- [x] [P3-T1] Update the safe-mode branch of `ResponseShaper.ShapeEvent` in `src/OpenClaw.MailBridge/ResponseShaper.cs` (lines 53–58) to additionally null `RequiredAttendeesJson`, `OptionalAttendeesJson`, and `ResourcesJson`, alongside the existing `BodyPreview`/`BodyFull` nulling and `IsRedacted = true`. Update the method comment to state attendee-PII redaction parity with `ShapeMessage`.
  - Acceptance: safe-mode branch nulls all three attendee fields; enhanced-mode branch leaves them intact. (US-AC4, SP security)
- [x] [P3-T2] Add a unit test to `tests/OpenClaw.MailBridge.Tests/ResponseShaperTests.cs` (or `ResponseShaperEventBodyFullTests.cs`) asserting that `ShapeEvent` in safe mode nulls all three attendee JSON fields and sets `IsRedacted = true`, using an `EventDto` whose three fields are populated.
  - Acceptance: test passes; all three fields null in safe mode. (US-AC4)
- [x] [P3-T3] Add a unit test asserting that `ShapeEvent` in enhanced mode preserves all three populated attendee JSON fields unchanged.
  - Acceptance: test passes; enhanced mode does not null the attendee fields. (US-AC4 non-regression)

### Phase 4 — Final QA Loop & Coverage Verification

Run the full C# toolchain in order. If any step fails or changes files, restart from [P4-T1].

- [x] [P4-T1] Run formatting: `dotnet csharpier .` (or `csharpier .`) from repo root, then re-run `dotnet csharpier check .` to confirm a clean pass.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/final-format.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:` (0), and `Output Summary:` (no files need formatting). If files changed, restart from [P4-T1]. (SP-DoD7)
- [x] [P4-T2] Run linting/analyzers: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/final-build.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:` (0), and `Output Summary:` (0 analyzer errors). (SP-DoD7)
- [x] [P4-T3] Run type checking (nullable): `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/final-nullable.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:` (0), and `Output Summary:` (0 nullable warnings-as-errors). (SP-DoD7)
- [x] [P4-T4] Verify architecture/COM-confinement: confirm no new `ProjectReference` edges were added and all Outlook COM access remains inside `OpenClaw.MailBridge` (the new partial is in that project; no COM types leaked to other projects).
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/final-architecture.<timestamp>.md` records the project-graph review result and confirms COM confinement holds. (architecture-boundaries)
- [x] [P4-T5] Run tests with coverage: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/final-test.<timestamp>.md` records `Timestamp:`, `Command:`, `EXIT_CODE:` (0), and `Output Summary:` with numeric post-change line coverage % and branch coverage %, and the total/passed test counts. All tests pass. (SP-DoD3, SP-DoD7)
- [x] [P4-T6] Compute and record the coverage delta and threshold verdict: compare baseline coverage ([P0-T5]) against post-change coverage ([P4-T5]); report changed-code coverage for the new/modified files (`OutlookScanner.Attendees.cs`, `OutlookScanner.GraphFields.cs`, `ResponseShaper.cs`).
  - Acceptance: `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/coverage-delta.<timestamp>.md` reports baseline %, post-change %, changed-code %; confirms line >= 85%, branch >= 75%, and no regression on changed lines. If any threshold fails, outcome is remediation-required (not PASS). (US-AC6)
- [x] [P4-T7] Confirm `EventDto` contract shape is unchanged: verify `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` `EventDto` positional argument count and order are identical to the pre-change state (only the three `null` literals in `BuildEventDto` became populated expressions).
  - Acceptance: `evidence/qa-gates/contract-unchanged.<timestamp>.md` records the verification (diff scope confined to `OutlookScanner.*` and `ResponseShaper.cs`; no change to `BridgeContracts.cs`). (US-AC6, SP versioning)

## Acceptance-Criteria → Task Map

| Criterion | Tasks |
|---|---|
| US-AC1 (non-null correct attendees, enhanced mode) | P2-T1, P2-T3, P2-T4, P2-T7 |
| US-AC2 (Graph `{"name","email"}` shape, lowercase keys, order) | P1-T1, P1-T2, P1-T3 |
| US-AC3 (per-type classification; out-of-range excluded) | P2-T1, P2-T8 |
| US-AC4 (safe-mode nulls all three; enhanced preserves) | P3-T1, P3-T2, P3-T3 |
| US-AC5 (missing name/email -> empty string, both keys) | P1-T4, P2-T9 |
| US-AC6 (no contract change; coverage thresholds; no regression) | P2-T4, P4-T6, P4-T7 |
| SP-DoD1 (AC mapped to tests) | this map; Phase 1–3 test tasks |
| SP-DoD2 (behavior matches AC) | P2-T7, P2-T8, P2-T9, P2-T10, P3-T2, P3-T3 |
| SP-DoD3 (tests added) | P1-T2..T4, P2-T7..T10, P3-T2, P3-T3, P4-T5 |
| SP-DoD4 (edge/error cases tested) | P1-T3, P1-T4, P2-T8, P2-T9, P2-T10 |
| SP-DoD5 (docs updated if applicable) | P2-T5 (XML doc); README/feature-doc N/A (no public-surface change) |
| SP-DoD6 (telemetry/logging) | N/A per spec line 104 (no telemetry change); covered by no-op confirmation in P4-T2 |
| SP-DoD7 (toolchain pass) | P4-T1, P4-T2, P4-T3, P4-T5 |
| SP-B1 (empty -> `[]`, not null) | P1-T1, P1-T3, P2-T3, P2-T10 |
| SP-B2 (out-of-range Type ignored) | P2-T1, P2-T8 |
| SP-B3 (per-recipient fail-soft) | P2-T1 |
| SP-B4 (deterministic COM release) | P2-T2 |
| SP-B5 (ProtectedFieldsAvailable unchanged) | P2-T5 |

## Test Plan

- **Unit (pure):** JSON shaping — Graph shape/lowercase keys/order (P1-T2), empty -> `"[]"` (P1-T3), missing name/email -> `""` with both keys (P1-T4).
- **Unit (scanner/COM seam):** enhanced-mode population with correct names/emails (P2-T7), per-type classification + out-of-range exclusion (P2-T8), missing values via scanner path (P2-T9), empty/absent collection -> `"[]"` (P2-T10). COM enumeration exercised through the reflection-readable `FakeAppointmentItem.Recipients` double (P2-T6); no live COM, no temp files.
- **Unit (redaction):** safe mode nulls all three (P3-T2), enhanced mode preserves all three (P3-T3).
- **Integration:** none required; behavior is verifiable through the existing scanner test seam. No new external-system interaction is introduced.
- **Coverage evidence:** baseline `evidence/baseline/baseline-test.<timestamp>.md` (P0-T5); post-change `evidence/qa-gates/final-test.<timestamp>.md` (P4-T5); comparison `evidence/qa-gates/coverage-delta.<timestamp>.md` (P4-T6).

## Open Questions / Notes

- The spec open question (empty array vs null) is resolved in design decision 5 / SP-B1: `"[]"` for a type with no recipients; `null` reserved for safe-mode redaction and unread state. Confirm during P2-T10 that downstream consumers can distinguish these two states.
- The `ProtectedFieldsAvailable` question (SP-B5) is resolved in design decision 7: the body-derived signal is preserved unchanged; attendee PII is protected by safe-mode redaction only.
- If `OutlookScanner.GraphFields.cs` or any test file approaches the 500-line cap during implementation, split into the named sibling partial/test file rather than exceeding the cap.
