# Code Review: mailbridge-event-attendees-json (#71)

**Review Date:** 2026-06-13
**Reviewer:** feature-review agent
**Feature Folder:** `docs/features/active/mailbridge-event-attendees-json-71`
**Feature Folder Selection Rule:** Folder suffix `-71` matches the canonical issue number and holds the material scoping-doc changes (spec.md, user-story.md).
**Base Branch:** `main` (merge-base `c0fa1024f61eac924331ac3757f1acbc5d724b03`)
**Head Branch:** `open-claw-bridge-wt-2026-06-13-10-27` (`65a5f8d5a750bf11166d4469ade4d6d7a0921ce8`)
**Review Type:** Initial review

---

## Executive Summary

The change populates the three `EventDto` attendee JSON fields from the COM `AppointmentItem.Recipients` collection and extends safe-mode redaction to null those fields. It is implemented as a new partial-class file (`OutlookScanner.Attendees.cs`) that cleanly separates a pure, COM-free JSON shaping function (`ShapeAttendeeJson`) from the COM enumeration method (`ReadAttendees`), plus a one-line wiring change in `BuildEventDto`, a three-line safe-mode redaction addition in `ResponseShaper.ShapeEvent`, and a small fail-soft COM helper (`GetOptionalIndexedItem`).

**What changed:**
- `OutlookScanner.Attendees.cs` (new, 179 lines): `AttendeeJsonSet`/`Attendee` records, static `JsonSerializerOptions`, pure `ShapeAttendeeJson`/`SerializeAttendees`, and the COM `ReadAttendees` single-pass enumerator with per-recipient and collection-level deterministic release.
- `OutlookScanner.GraphFields.cs` (modified): three `null` literals replaced by `attendees.RequiredJson/OptionalJson/ResourcesJson`; XML note clarifying `ProtectedFieldsAvailable` is unchanged.
- `ResponseShaper.cs` (modified): safe-mode branch nulls the three attendee fields.
- `OutlookComHelpers.cs` (modified): `GetOptionalIndexedItem` fail-soft 1-based accessor.
- 4 test files: 12 new tests across pure shaping, scanner path (incl. fail-soft), and redaction.

Scope is C# only. Toolchain (format, analyzers, nullable, architecture, tests + coverage) passed in the executor run and was re-verified here against the diff and the cobertura coverage file. Changed production code is at 100% line/branch.

**Top 3 risks:**
1. The broad `catch` in `GetOptionalIndexedItem` swallows all exceptions to fail soft. This is consistent with the existing `OutlookComHelpers` idiom and confined to the COM adapter boundary, but it is a deliberate broad catch worth noting.
2. Email resolution falls back from `Recipient.Address` to `AddressEntry.Address` only; Exchange DN-to-SMTP translation beyond the COM surface is explicitly a non-goal, so some Exchange recipients may yield a non-SMTP address string. This matches the spec non-goal and is acceptable.
3. Coverage was verified from `tests/**/TestResults/.../coverage.cobertura.xml` rather than the SKILL's nominal `artifacts/csharp/coverage.xml`; the numbers were confirmed by direct parse.

**PR readiness recommendation:** **Go** — implementation matches the spec and AC, toolchain is clean, changed code is fully covered, and architecture/COM/contract boundaries are preserved.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Info | `src/OpenClaw.MailBridge/OutlookComHelpers.cs` | `GetOptionalIndexedItem` (lines ~38–47) | Broad `catch` swallows all exceptions to return `null` (fail soft). | No change required; the catch is intentional, documented, and matches the existing `GetOptional*` idiom for the COM boundary. | A broad catch is normally discouraged, but per spec SP-B3 a single unreadable recipient must not abort the scan; the catch is confined to the COM adapter. | Diff inspection; catch path covered by `ScanCalendar_should_fail_soft_when_a_recipient_read_throws` (cobertura lines 43–45 hit). |
| Info | `src/OpenClaw.MailBridge/OutlookScanner.Attendees.cs` | `ReadAttendees` email fallback (lines ~151–162) | Email resolution uses `Recipient.Address` then `AddressEntry.Address`; no Exchange DN-to-SMTP translation. | None; this is an explicit spec non-goal. | Some Exchange recipients may carry a legacyExchangeDN-style address; acceptable per user-story Non-Goals. | `user-story.md` Non-Goals; diff inspection. |
| Info | n/a | coverage artifact path | C# coverage verified from `TestResults/.../coverage.cobertura.xml`, not `artifacts/csharp/coverage.xml`. | None; this is the repo's canonical coverlet output per `csharp.md`. | Documents the evidence source for traceability. | Direct parse of cobertura (line-rate 0.9407, branch-rate 0.8654). |

No Blockers or Major findings.

---

## Implementation Audit

### C# implementation audit

#### What changed well

- Clean separation of a pure, COM-free `ShapeAttendeeJson` (directly unit-testable) from the COM-bound `ReadAttendees`. This follows the repository's "isolate I/O from pure logic" principle and the DI/seam guidance in `csharp.md`.
- Single-pass enumeration over `Recipients` with a `switch` on `Type` (1/2/3) and explicit exclusion of out-of-range values, matching spec SP-B2.
- Deterministic COM release: each `Recipient` and any resolved `AddressEntry` are released per-iteration in a `finally`, and the `Recipients` collection is released in the outer `finally`, following the `GetStoreId` idiom and the COM-confinement rule. No RCW accumulation.
- The null-vs-`"[]"` distinction is deliberate and documented: the scanner emits `"[]"` for a type with no recipients, and null is reserved for safe-mode redaction. This makes "no attendees" distinguishable from "redacted" at the consumer.
- A single static `JsonSerializerOptions` avoids per-call allocation and guarantees deterministic output; lowercase `name`/`email` keys are enforced via `JsonPropertyName` on the `AttendeeJson` projection, matching the Graph `emailAddress` shape.

#### Type safety and API notes

- Nullable reference types respected: optional COM reads return `string?`/`object?`; missing values coalesce to `string.Empty` so each serialized object always carries both keys. Build with `TreatWarningsAsErrors=true` passed with 0 warnings.
- New public/cross-boundary surface is minimal and correct: `EventDto` is unchanged (same positional argument count/order), so no contract break. New types are `internal`/`private`; only `ShapeAttendeeJson`/`Attendee`/`AttendeeJsonSet` are `internal` to enable direct unit testing without live COM.
- No COM types leak across project boundaries; the carrier records are plain managed values.

#### Error handling and logging

- Fail-soft per recipient via `GetOptional*`/`GetOptionalIndexedItem`; the scan completes even when an individual recipient read throws. The broad catch is confined to the COM adapter boundary and is documented.
- No new logging added, consistent with the spec ("none beyond existing scan logging").

---

## Test Quality Audit

The change adds 12 focused tests across three concerns. They are deterministic (injected clock `() => FixedNow`), isolated (per-test fakes, no shared state), and free of external dependencies or temporary files. The COM boundary is substituted with reflection-readable fakes, avoiding live Outlook.

### Reviewed test and QA artifacts

- `tests/OpenClaw.MailBridge.Tests/OutlookScannerAttendeesShapeTests.cs` — verifies lowercase keys, order preservation, `"[]"` for empty groups, and both-keys-present on missing values. Asserts exact JSON strings.
- `tests/OpenClaw.MailBridge.Tests/OutlookScannerAttendeesTests.cs` — verifies enhanced-mode population, per-type classification with out-of-range exclusion, missing name/email, AddressEntry fallback, empty/absent collection -> `"[]"`, and fail-soft on a throwing collection.
- `tests/OpenClaw.MailBridge.Tests/ResponseShaperEventBodyFullTests.cs` — verifies safe-mode nulls all three fields and enhanced mode preserves them.
- `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/final-test.2026-06-13T14-41.md` — 474 passed, 0 failed; coverage 94.07% line / 86.54% branch.
- `docs/features/active/mailbridge-event-attendees-json-71/evidence/qa-gates/coverage-delta.2026-06-13T14-41.md` — per-file 100% line/branch on changed code; no regression.
- `tests/OpenClaw.MailBridge.Tests/TestResults/82e0c0f9-12e0-4ea3-9f69-873a62abd6dc/coverage.cobertura.xml` — directly parsed: solution line-rate 0.9407, branch-rate 0.8654; changed prod files at 1.0/1.0.

### Quality assessment prompts

- **Determinism:** Injected fixed clock; static serializer options; no RNG, sleeps, or wall-clock reads (verified by grep on the test diff).
- **Isolation:** Each test builds its own scanner, COM fake, and repository.
- **Speed:** In-memory fakes only; solution test gate completed with EXIT 0.
- **Diagnostics:** FluentAssertions with rationale strings produce actionable failure messages.

---

## Security / Correctness Checks

| Check | Status | Evidence |
|---|---|---|
| No secrets in code | ✅ PASS | Diff inspection; only attendee display name/email projection, no credentials. |
| No unsafe subprocess or command construction | ✅ PASS | No process or shell invocation introduced. |
| Input validation at boundaries | ✅ PASS | COM reads are optional/guarded; out-of-range `Type` excluded; null collection handled. |
| Error handling remains explicit | ✅ PASS | Fail-soft is intentional and documented; no silent swallowing in pure logic. |
| Configuration / path handling is safe | ✅ PASS | No new config or path handling; behavior gated by existing `BridgeSettings.Mode`. |
| PII redaction in safe mode | ✅ PASS | Safe-mode branch nulls all three attendee fields; verified by `ShapeEvent_in_safe_mode_should_null_all_three_attendee_fields`. |

---

## Research Log

No external research was required. All findings are grounded in the branch diff, repository rule files, and the feature-folder evidence artifacts.

---

## Verdict

The implementation is ready for normal PR flow. It satisfies the documented behavior and acceptance criteria, keeps Outlook COM confined to `OpenClaw.MailBridge` with deterministic release, preserves the `EventDto` contract, and is fully covered on changed lines with no coverage regression. The only findings are Informational (a documented fail-soft broad catch, an explicitly out-of-scope Exchange DN-to-SMTP limitation, and the coverage-artifact path note); none block merge. Recommendation: **Go**.
