# mailbridge-messagedto-resolved-fields - Plan

- **Issue:** #73
- **Parent (optional):** Deferred from #70; final issue of Track M (#72 -> #71 -> #73)
- **Owner:** drmoisan
- **Last Updated:** 2026-06-13T13-34
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature

## Authoritative Inputs

- Spec (Approved v1.0, decisions locked): `docs/features/active/mailbridge-messagedto-resolved-fields-73/spec.md`
- User story: `docs/features/active/mailbridge-messagedto-resolved-fields-73/user-story.md`
- Research (file:line evidence): `artifacts/research/2026-06-13-issue-73-messagedto-resolved-fields.md`

## Policy References (read in order, do not duplicate content)

1. `CLAUDE.md` (standing instructions)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/csharp.md`
5. `.claude/rules/architecture-boundaries.md`
6. `.claude/rules/quality-tiers.md`

All work must comply with these policies. The only language in scope is C# (.NET 10). PowerShell, Python, and TypeScript are out of scope for this feature.

## Locked Decisions (do not re-litigate)

- **D-A:** `FromEmailAddress` = `SentOnBehalfOfEmailAddress` SMTP-resolved; fallback to resolved sender.
- **D-B:** `MeetingMessageType` is `int?` (raw `OlMeetingType`), null for ordinary mail; Graph-string conversion lives in `SchedulingDtoMapper`.
- **D-C:** SMTP resolution is fail-soft: attempt true SMTP (`PropertyAccessor PR_SMTP_ADDRESS` or `GetExchangeUser().PrimarySmtpAddress`) inside try/catch; fallback `AddressEntry.Address` then raw `SenderEmailAddress`/`SentOnBehalfOf`; never throw out of resolution.
- **D-D:** Introduce a unifying `IMessageSource` interface plus a COM data-type adapter that maps the COM `MailItem`/`MeetingItem` onto it. Core normalization, `SchedulingDtoMapper`, and both cache repositories depend on the abstraction / contract-shaped data, not on concrete COM types. The interface and COM adapter live INSIDE `OpenClaw.MailBridge` so Outlook COM stays confined (architecture-boundaries rule 1). A future Modern/Graph model must be addable by writing only a second adapter. Keep the seam minimal and purpose-specific.

## Evidence Location Invariant

All evidence artifacts use the canonical scheme `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/<kind>/`. Writing under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/coverage/`, or any other non-canonical path is a policy violation. Canonical kinds used in this plan: `baseline`, `qa-gates`, `regression-testing`, `other`.

## Per-Task C# Toolchain Gate (applies to every implementation task that changes `*.cs`)

Per `.claude/rules/csharp.md`, after each implementation task run, in order, restarting from step 1 if any step changes files or fails:

1. **Format**: `csharpier format .` (global CSharpier 1.3.0; the `dotnet csharpier` driver is not runnable in this repo — no working local tool manifest — and CSharpier 1.x uses subcommand syntax).
2. **Lint/analyzers**: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
3. **Nullable/type**: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
4. **Architecture**: verify project-reference + COM-confinement boundaries in `.claude/rules/architecture-boundaries.md` (review of the project graph; no disallowed `ProjectReference` and no Outlook COM outside `OpenClaw.MailBridge`).
5. **Test**: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`

A task is complete only when its stated edit is in place, the file stays within the 500-line cap, and the gate passes. The full coverage-bearing QA loop with numeric evidence is captured once in the final phase (Phase 7).

---

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture and Policy Read

- [x] [P0-T1] Read the policy files in the required order and record the read.
  - Files read: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md`.
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/baseline/phase0-instructions-read.md` exists and contains `Timestamp:`, `Policy Order:`, and the explicit list of files read.

- [x] [P0-T2] Capture the baseline C# format state.
  - Command: `csharpier check .` (global CSharpier 1.3.0 subcommand form)
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/baseline/baseline-csharpier.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (formatted-clean status / any pre-existing diffs).

- [x] [P0-T3] Capture the baseline analyzer + nullable build state.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true`
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/baseline/baseline-build.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warning/error counts).

- [x] [P0-T4] Capture the baseline test + coverage state.
  - Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/baseline/baseline-test.md` exists with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including numeric baseline **line %** and **branch %** for `OpenClaw.MailBridge` and `OpenClaw.Core`, and the passed/total test count.

### Phase 1 — Contract Extension (MessageDto)

Lands before all consumers (sequencing requirement). Supports AC-01.

- [x] [P1-T1] Add four trailing optional parameters to `MessageDto` in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` (record at lines 74-92).
  - Edit: append, after `bool IsRedacted` (line 91) and before the closing `)`, the parameters `string? SenderEmailResolved = null, string? FromEmailAddress = null, string? ConversationId = null, int? MeetingMessageType = null`. Convert `IsRedacted` to `bool IsRedacted = false` only if required to keep all-trailing-optional ordering valid; otherwise keep the four new params strictly after the existing positional list. `ToJson` (line 87) and `CcJson` (line 88) retain their positions.
  - Acceptance: `MessageDto` compiles; all existing construction sites (scanner, both repositories, tests) remain valid with no positional change to `ToJson`/`CcJson`; per-task C# gate passes. (AC-01)

### Phase 2 — Message-Source Abstraction and COM Adapter (D-D)

Lands before normalization is re-routed. Supports AC-09.

- [x] [P2-T1] Create the unifying interface `IMessageSource` in a new file `src/OpenClaw.MailBridge/IMessageSource.cs`.
  - Edit: define `internal interface IMessageSource` exposing only the data the normalizer needs: `string? SenderEmailResolved`, `string? FromEmailAddress`, `string? ConversationId`, `int? MeetingMessageType`, and recipient access shaped for `ToJson`/`CcJson` (e.g. `IReadOnlyList<Attendee> ToRecipients` and `IReadOnlyList<Attendee> CcRecipients`, reusing the existing `OutlookScanner.Attendee` shape). Interface is purpose-specific and minimal; no COM types appear on the interface surface.
  - Acceptance: interface file under 500 lines; no `Microsoft.Office.Interop` or `Marshal` reference on the interface; per-task C# gate passes. (AC-09)

- [x] [P2-T2] Create the COM adapter `ComMessageSource` in a new file `src/OpenClaw.MailBridge/ComMessageSource.cs` implementing `IMessageSource`.
  - Edit: adapter wraps the late-bound COM `MailItem`/`MeetingItem` (`object item`) plus the `ComActiveObject` (`_com`) for deterministic release, and maps the COM surface onto `IMessageSource`. SMTP resolution and recipient enumeration logic added in Phase 3 will live here. This file is the only place that reads concrete COM members for these fields, keeping COM confined to `OpenClaw.MailBridge`.
  - Acceptance: adapter file under 500 lines; implements every `IMessageSource` member; per-task C# gate passes. (AC-09)

- [x] [P2-T3] Add an adapter unit test for the pure (non-COM) mapping surface in a new file `tests/OpenClaw.MailBridge.Tests/ComMessageSourceTests.cs`.
  - Edit: using the reflection-based fakes (extended in Phase 6) where COM enumeration is exercised, assert that the adapter projects sender/from/conversation/meeting-type and To/Cc recipient lists from a fake source. Add the minimal mapping cases that do not require the SMTP/recipient COM logic (those are validated in Phase 3 tests); this task establishes the adapter-contract test class.
  - Acceptance: test class compiles and runs; MSTest + FluentAssertions; deterministic; no temp files; no real COM; per-task C# gate passes. (AC-09)

### Phase 3 — Bridge Normalization (OutlookScanner)

Supports AC-02, AC-03, AC-04, AC-05, AC-06, AC-07. COM-confined (T3) work inside `OpenClaw.MailBridge`.

- [x] [P3-T1] Add the fail-soft SMTP sender-resolution helper to `src/OpenClaw.MailBridge/ComMessageSource.cs`.
  - Edit: implement `SenderEmailResolved` resolution per D-C: attempt true SMTP first (`Sender.AddressEntry` `PropertyAccessor PR_SMTP_ADDRESS` or `GetExchangeUser().PrimarySmtpAddress`) inside try/catch, then fall back to `Sender.Address` -> `Sender.AddressEntry.Address` -> raw `SenderEmailAddress`. Never throw out of resolution. Release every COM wrapper obtained (`Sender`, `AddressEntry`, exchange user) in `finally` via `_com.ReleaseAll`, following the `ReadAttendees`/`GetStoreId` idiom.
  - Acceptance: resolution returns a value or null without throwing for non-Exchange/failure inputs; COM released in `finally`; per-task C# gate passes. (AC-02)

- [x] [P3-T2] Add the fail-soft `FromEmailAddress` resolution to `src/OpenClaw.MailBridge/ComMessageSource.cs`.
  - Edit: implement `FromEmailAddress` per D-A: SMTP-resolve `SentOnBehalfOfEmailAddress` using the same fail-soft mechanism as P3-T1; when no on-behalf-of identity is present, fall back to the resolved sender from P3-T1. Do not alias `SenderEmailResolved` outright.
  - Acceptance: delegate-sent input yields the on-behalf-of identity; non-delegate input yields the resolved sender; no throw on failure; per-task C# gate passes. (AC-03)

- [x] [P3-T3] Add message-recipient enumeration to `src/OpenClaw.MailBridge/OutlookScanner.Attendees.cs` (reusing the #71 serializer).
  - Edit: add a `ReadMessageRecipients(object item)` method that enumerates the COM `Recipients` collection in one pass, classifies by `Type` (1 -> To list, 2 -> Cc list; 3/Bcc ignored), reuses `Attendee`/`AttendeeJsonOptions`/`SerializeAttendees`, fails soft per recipient, and releases all COM wrappers in `finally` via `_com.ReleaseAll`. Return a named carrier (e.g. `RecipientJsonSet(ToJson, CcJson)`) or named tuple; a null/empty `Recipients` collection yields `"[]"` for both (never null).
  - Acceptance: file stays under 500 lines (currently 179 lines); To=type 1, Cc=type 2; empty/absent recipients -> `"[]"`; per-task C# gate passes. (AC-04)

- [x] [P3-T4] Read `ConversationID` and `MeetingType` in `src/OpenClaw.MailBridge/ComMessageSource.cs`.
  - Edit: `ConversationId` via `OutlookComHelpers.GetOptionalString(item, "ConversationID")` (stored unmodified). `MeetingMessageType` via `OutlookComHelpers.GetOptionalInt(item, "MeetingType")` for meeting items only; ordinary `MailItem` yields null (D-B).
  - Acceptance: meeting item exposes raw `OlMeetingType` int; ordinary mail yields null; ConversationId passed through unchanged; per-task C# gate passes. (AC-05, AC-06)

- [x] [P3-T5] Route `MessageDto` construction through `IMessageSource`/`ComMessageSource` in `src/OpenClaw.MailBridge/OutlookScanner.cs` (`NormalizeMessage`, lines 385-429).
  - Edit: construct a `ComMessageSource` (or via a small factory) from `item` and `_com`; replace the hardcoded `null, null` for `ToJson`/`CcJson` (lines 419-420) with the values from P3-T3; pass `SenderEmailResolved`, `FromEmailAddress`, `ConversationId`, `MeetingMessageType` from the abstraction into the new trailing parameters. `NormalizeMessage` and downstream normalization depend on `IMessageSource`, not concrete COM types. Keep `OutlookScanner.cs` within the 500-line cap (currently at the cap) by placing any net-new helper bodies in `OutlookScanner.Attendees.cs` (P3-T3) or `ComMessageSource.cs`.
  - Acceptance: `NormalizeMessage` populates all six fields via the abstraction; COM released deterministically in `finally`; `OutlookScanner.cs` does not exceed 500 lines; per-task C# gate passes. (AC-04, AC-05, AC-06, AC-07, AC-09)

### Phase 4 — Scheduling DTO Mapper (OpenClaw.Core, T1)

Supports AC-10. Consumes contract-shaped data only (no COM).

- [x] [P4-T1] Wire resolved sender/from/conversation in `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs` (`MapMessage`, lines 32-56).
  - Edit: build `Sender` from `message.SenderName` + `message.SenderEmailResolved` (line 36 currently uses `SenderEmail`); build a separate `From` attendee from `message.SenderName` + `message.FromEmailAddress`; set `From:` and `Sender:` (lines 46-47) to these distinct values; replace the hardcoded `ConversationId: null` (line 51) with `message.ConversationId`. Keep `BuildAttendee` null-on-empty behavior.
  - Acceptance: `Sender.Email` = resolved sender; `From.Email` = from address; `ConversationId` flows from the DTO; per-task C# gate passes. (AC-10)

- [x] [P4-T2] Replace the hardcoded meeting-type string with an int->Graph-string switch in `src/OpenClaw.Core/Agent/Runtime/SchedulingDtoMapper.cs`.
  - Edit: add a private `MapMeetingMessageType(int? type)` mirroring `MapSensitivity` (lines 150-158) and `MapImportance` (lines 160-167): `0 -> "meetingRequest"`, `1 -> "meetingCancelled"`, `2 -> "meetingDeclined"`, `3 -> "meetingAccepted"`, `4 -> "meetingTentativelyAccepted"`, `_ -> null`. Replace `MeetingMessageType: message.ItemKind == "meeting" ? "meetingRequest" : null` (line 53) with `MeetingMessageType: MapMeetingMessageType(message.MeetingMessageType)`.
  - Acceptance: each `OlMeetingType` int maps to the correct Graph string; null/unknown yields null; per-task C# gate passes. (AC-10)

- [x] [P4-T3] Extend `tests/OpenClaw.Core.Tests/Agent/Runtime/SchedulingDtoMapperTests.cs` for the new wiring.
  - Edit: update `MapMessage_DeferredFields_AreNull` (lines 126-133) for the new behavior; add `[DataTestMethod]`/`[DataRow]` cases for each `OlMeetingType` int -> Graph string; add tests asserting `ConversationId` flows from the DTO and that `From.Email`/`Sender.Email` reflect `FromEmailAddress`/`SenderEmailResolved` distinctly.
  - Acceptance: MSTest + FluentAssertions; deterministic; no temp files; covers each enum value and the null/ordinary-mail case; per-task C# gate passes. (AC-08, AC-10)

### Phase 5 — Cache Persistence (both repositories)

Supports AC-10. Idempotent migrations mirror the #71/#72 events pattern. Additions go in partial files to respect the 500-line cap.

- [x] [P5-T1] Add the four new `messages` columns and an idempotent migration to `src/OpenClaw.MailBridge/CacheRepository.Schema.cs`.
  - Edit: extend the `CREATE TABLE IF NOT EXISTS messages(...)` DDL (line 14) with `sender_email_resolved TEXT NULL, from_email_address TEXT NULL, conversation_id TEXT NULL, meeting_message_type INTEGER NULL`; add a `MessageFieldColumns` `(Name, Definition)[]` and a `MigrateMessagesSchemaAsync` + `MessagesColumnExistsAsync` (`PRAGMA table_info(messages)` guard) mirroring `MigrateEventsSchemaAsync`/`EventsColumnExistsAsync` (lines 41-79).
  - Acceptance: migration is idempotent (running twice adds no duplicate column); file under 500 lines; per-task C# gate passes. (AC-10)

- [x] [P5-T2] Call `MigrateMessagesSchemaAsync` from `InitializeAsync` in `src/OpenClaw.MailBridge/CacheRepository.cs` (line 86-94).
  - Edit: after `await MigrateEventsSchemaAsync(conn);` (line 93) add `await MigrateMessagesSchemaAsync(conn);`.
  - Acceptance: initialization runs both migrations; per-task C# gate passes. (AC-10)

- [x] [P5-T3] Update bridge INSERT/UPSERT SQL and parameter binding in `src/OpenClaw.MailBridge/CacheRepository.cs`.
  - Edit: extend `UpsertMessageAsync` SQL (lines 128-157) INSERT column list, VALUES list, and `ON CONFLICT ... DO UPDATE SET` with the four new columns; extend `AddMessageParameters` (lines 354-390) with `$sender_email_resolved`, `$from_email_address`, `$conversation_id` (`(object?)... ?? DBNull.Value`) and `$meeting_message_type` (`ToDbValue(message.MeetingMessageType)`).
  - Acceptance: SQL and parameters cover all four columns; `CacheRepository.cs` stays under 500 lines (currently 460 lines) — if the additions would exceed the cap, move `AddMessageParameters` into `CacheRepository.Readers.cs` or a new partial; per-task C# gate passes. (AC-10)

- [x] [P5-T4] Update the bridge reader in `src/OpenClaw.MailBridge/CacheRepository.Readers.cs` (`ReadMessage`, lines 14-33).
  - Edit: add four trailing reads to the `MessageDto` construction using named arguments mirroring `ReadEvent`: `SenderEmailResolved: GetString(reader, "sender_email_resolved")`, `FromEmailAddress: GetString(reader, "from_email_address")`, `ConversationId: GetString(reader, "conversation_id")`, `MeetingMessageType: GetNullableInt(reader, "meeting_message_type")`. (`SELECT *` already returns the new columns.)
  - Acceptance: round-trip reads the four new values; per-task C# gate passes. (AC-10)

- [x] [P5-T5] Add the four new `messages` columns and idempotent migration to `src/OpenClaw.Core/CoreCacheRepository.Schema.cs`.
  - Edit: extend the `CREATE TABLE IF NOT EXISTS messages(...)` DDL (lines 28-51) with the same four columns; add a `MessageFieldColumns` array and `MigrateMessagesSchemaAsync` + `MessagesColumnExistsAsync` (`PRAGMA table_info(messages)` guard) mirroring the events migration (lines 107-157).
  - Acceptance: migration idempotent; file under 500 lines; per-task C# gate passes. (AC-10)

- [x] [P5-T6] Call the Core messages migration from initialization and update Core INSERT/UPSERT SQL + binding in `src/OpenClaw.Core/CoreCacheRepository.cs`.
  - Edit: invoke `MigrateMessagesSchemaAsync` at the Core initialization site (alongside the existing events migration call); extend `UpsertMessagesAsync` SQL (lines 96-128) INSERT/VALUES/`ON CONFLICT DO UPDATE SET` with the four new columns; extend `AddMessageParameters` (lines 424-475) with the four bindings (string columns `(object?)... ?? DBNull.Value`; `meeting_message_type` via `ToDbValue`).
  - Acceptance: SQL and parameters cover all four columns; `CoreCacheRepository.cs` does not grow over the cap — net-new bodies go into partials (`CoreCacheRepository.Schema.cs` or a new partial), not the already-large main file; per-task C# gate passes. (AC-10)

- [x] [P5-T7] Update the Core reader in `src/OpenClaw.Core/CoreCacheRepository.cs` (`ReadMessage`, lines 607-626).
  - Edit: add four trailing named reads to the `MessageDto` construction: `SenderEmailResolved: ReadString(reader, "sender_email_resolved")`, `FromEmailAddress: ReadString(reader, "from_email_address")`, `ConversationId: ReadString(reader, "conversation_id")`, `MeetingMessageType: ReadNullableInt(reader, "meeting_message_type")`. (`SELECT *` already returns the new columns.)
  - Acceptance: round-trip reads the four new values; per-task C# gate passes. (AC-10)

- [x] [P5-T8] Add bridge cache round-trip and idempotent-migration tests.
  - Edit: in `tests/OpenClaw.MailBridge.Tests/CacheRepositoryGraphFieldsTests.cs` (or a new `CacheRepositoryMessageFieldsTests.cs`), add tests that upsert a `MessageDto` carrying all four new values and assert `GetMessageAsync` reads them back; add a test that runs initialization/migration twice and confirms no error and stable schema (`PRAGMA table_info` guard).
  - Acceptance: MSTest + FluentAssertions; deterministic; no temp files; covers persist + read-back + idempotent migration; per-task C# gate passes. (AC-08, AC-10)

- [x] [P5-T9] Add Core cache round-trip and idempotent-migration tests.
  - Edit: in the Core cache test suite (`tests/OpenClaw.Core.Tests/...`, mirroring the existing message/event cache tests), add tests that upsert a `MessageDto` with all four new values and read them back, plus a double-migration idempotency test against the Core `messages` table.
  - Acceptance: MSTest + FluentAssertions; deterministic; no temp files; covers persist + read-back + idempotent migration; per-task C# gate passes. (AC-08, AC-10)

### Phase 6 — Scanner Field Tests (meeting and ordinary-mail paths)

Supports AC-02..AC-08. Uses the reflection-based fakes; no real COM.

- [x] [P6-T1] Extend the reflection-based fakes in `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`.
  - Edit: add `ConversationID`, `SentOnBehalfOfEmailAddress`, and a `Sender` member (a `FakeSender`/`FakeRecipient`-shaped object exposing `Address`/`AddressEntry` for SMTP resolution) and a `Recipients` member to `FakeMailItem` (lines 85-98); add `ConversationID`, `MeetingType` (int), `SentOnBehalfOfEmailAddress`, `Sender`, and `Recipients` to `FakeMeetingItem` (lines 100-113). Reuse `FakeRecipients`/`FakeRecipient`/`FakeAddressEntry` (lines 151-197) for recipient enumeration; add a `FakeExchangeUser`-style member only if the SMTP true-resolution path is exercised reflectively.
  - Acceptance: fakes expose the new members; existing scanner tests still compile and pass; per-task C# gate passes. (AC-08)

- [x] [P6-T2] Add ordinary-mail-path tests in a new file `tests/OpenClaw.MailBridge.Tests/OutlookScannerMessageFieldsTests.cs`.
  - Edit: assert for a `FakeMailItem` with recipients that `ToJson`/`CcJson` are non-null `[{"name","email"}]` arrays (To=type 1, Cc=type 2), `ConversationId` non-empty, `MeetingMessageType` null, `SenderEmailResolved` resolved via the fail-soft chain, and `FromEmailAddress` falls back to the resolved sender when no on-behalf-of is set; add a missing/empty-`Recipients` case asserting `ToJson="[]"`, `CcJson="[]"`.
  - Acceptance: covers AC-02 fallback, AC-03 fallback, AC-04, AC-05, AC-06 (null) for ordinary mail; MSTest + FluentAssertions; deterministic; no temp files; no real COM; per-task C# gate passes. (AC-02, AC-03, AC-04, AC-05, AC-06, AC-08)

- [x] [P6-T3] Add meeting-message-path tests to `tests/OpenClaw.MailBridge.Tests/OutlookScannerMessageFieldsTests.cs`.
  - Edit: assert for a `FakeMeetingItem` (request) that `MeetingMessageType` carries the correct `OlMeetingType` int (0), `ConversationId` is non-empty, and `SenderEmailResolved`/`ToJson`/`ConversationId` are simultaneously valid (the AC-07 acceptance signal); add a cancellation case (`MeetingType` = 1) asserting the cancellation int; add a delegate-sent case asserting `FromEmailAddress` reflects the on-behalf-of identity (AC-03 present-branch); add an Exchange-DN sender case asserting `SenderEmailResolved` carries resolved SMTP, not the DN (AC-02 resolved-branch).
  - Acceptance: covers AC-02 resolved, AC-03 present, AC-06 (int), AC-07 for the meeting path; MSTest + FluentAssertions; deterministic; no temp files; no real COM; per-task C# gate passes. (AC-02, AC-03, AC-06, AC-07, AC-08)

### Phase 7 — Final QA Loop, Coverage, and Verification

Supports AC-11. Run the full seven-stage toolchain order; restart from step 1 if any step changes files or fails. All command tasks are unconditional; `SKIPPED` is not a valid completion.

- [x] [P7-T1] Run formatting and record evidence.
  - Command: `csharpier check .` (global CSharpier 1.3.0 subcommand form)
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/final-csharpier.md` with `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, `Output Summary:`; EXIT_CODE 0 (formatted clean).

- [x] [P7-T2] Run analyzer/lint build and record evidence.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/final-lint.md` with the schema fields; `Output Summary:` reports 0 analyzer errors and no new suppressions. (AC-11)

- [x] [P7-T3] Run nullable/type-check build and record evidence.
  - Command: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/final-typecheck.md` with the schema fields; `Output Summary:` reports 0 nullable warnings-as-errors. (AC-11)

- [x] [P7-T4] Verify architecture boundaries and COM confinement, and record evidence.
  - Check: confirm no new `ProjectReference` violates `.claude/rules/architecture-boundaries.md`; confirm `IMessageSource` and `ComMessageSource` reside only in `OpenClaw.MailBridge` and that `OpenClaw.Core`/mapper/`CoreCacheRepository` depend only on contract-shaped data (no `Microsoft.Office.Interop`/`Marshal` in their closure).
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/final-architecture.md` with `Timestamp:`, `Command:` (the grep/review commands used), `EXIT_CODE:`, `Output Summary:` stating COM stays confined and the project graph is unchanged in disallowed ways. (AC-09, AC-11)

- [x] [P7-T5] Run the full test suite with coverage and record numeric coverage evidence.
  - Command: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/final-test.md` with the schema fields; `Output Summary:` records post-change **line %** and **branch %** for `OpenClaw.MailBridge` and `OpenClaw.Core`, and passed/total tests; line >= 85% and branch >= 75%. (AC-08, AC-11)

- [x] [P7-T6] Verify coverage delta and changed-line no-regression, and record evidence.
  - Edit: compare Phase 0 baseline coverage (P0-T4) against Phase 7 post-change coverage (P7-T5); confirm no regression on changed lines (the touched files in Phases 1-6).
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/qa-gates/coverage-delta.md` reporting baseline coverage, post-change coverage, and changed-code coverage; verdict PASS only if line >= 85%, branch >= 75%, and no regression on changed lines. If any required numeric value is unavailable, the verdict is remediation-required, not PASS. (AC-11)

- [x] [P7-T7] Verify the 500-line file-size cap across all touched files and record evidence.
  - Check: confirm no production, test, or reusable script file touched in Phases 1-6 exceeds 500 lines (specifically `OutlookScanner.cs`, `OutlookScanner.Attendees.cs`, `CacheRepository.cs`, `CacheRepository.Schema.cs`, `CacheRepository.Readers.cs`, `CoreCacheRepository.cs`, `CoreCacheRepository.Schema.cs`, `IMessageSource.cs`, `ComMessageSource.cs`, and new test files).
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/other/file-size-check.md` with `Timestamp:`, `Command:` (line-count command), `EXIT_CODE:`, `Output Summary:` listing each touched file's line count, all <= 500. (AC-11)

- [x] [P7-T8] Verify acceptance-criteria coverage mapping and record evidence.
  - Edit: confirm each of AC-01..AC-11 maps to a completed task and at least one passing test or verification artifact above.
  - Acceptance: artifact `docs/features/active/mailbridge-messagedto-resolved-fields-73/evidence/other/ac-mapping.md` listing AC-01..AC-11 with the satisfying task IDs and evidence artifact paths; no AC unmapped.

---

## Acceptance Criteria Traceability

| AC | Tasks |
|---|---|
| AC-01 | P1-T1 |
| AC-02 | P3-T1, P3-T5, P6-T2, P6-T3 |
| AC-03 | P3-T2, P3-T5, P6-T2, P6-T3 |
| AC-04 | P3-T3, P3-T5, P6-T2 |
| AC-05 | P3-T4, P3-T5, P6-T2, P6-T3 |
| AC-06 | P3-T4, P3-T5, P6-T2, P6-T3 |
| AC-07 | P3-T5, P6-T3 |
| AC-08 | P2-T3, P4-T3, P5-T8, P5-T9, P6-T1, P6-T2, P6-T3 |
| AC-09 | P2-T1, P2-T2, P2-T3, P3-T5, P7-T4 |
| AC-10 | P4-T1, P4-T2, P4-T3, P5-T1..P5-T9 |
| AC-11 | P7-T1..P7-T8 |

## Sequencing Rationale

1. Contract first (Phase 1) so all consumers compile against the final `MessageDto` shape.
2. Abstraction + adapter (Phase 2) before normalization is re-routed (Phase 3), satisfying D-D.
3. Bridge normalization (Phase 3) populates the fields; mapper (Phase 4) and caches (Phase 5) consume them.
4. Scanner field tests (Phase 6) after the scanner and fakes support the new members.
5. Final coverage-bearing QA loop (Phase 7) verifies the seven-stage toolchain and AC-11 thresholds.

## Open Questions / Notes

- Research OQ-A resolved by D-A (FromEmailAddress reads SentOnBehalfOf, fallback to resolved sender).
- Research OQ-B resolved by D-B (`int?` in DTO; Graph string in mapper).
- Research OQ-C resolved by D-C (attempt true SMTP fail-soft, then AddressEntry.Address, then raw).
- File-size watch: `OutlookScanner.cs` is at the 500-line cap and `CoreCacheRepository.cs` is already large; all net-new bodies are placed in partials or new files per Phases 2/3/5.
