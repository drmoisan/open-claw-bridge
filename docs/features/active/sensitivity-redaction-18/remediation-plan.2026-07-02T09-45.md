# sensitivity-redaction — Remediation Plan (cycle 1)

- **Issue:** #18 (co-delivers #20)
- **Parent (optional):** none
- **Owner:** drmoisan
- **Last Updated:** 2026-07-02T09-45
- **Status:** Draft
- **Version:** 1.0
- **Work Mode:** full-feature (per `issue.md` metadata)
- **Remediation cycle:** 1 (entry timestamp 2026-07-02T09-45; branch head `d267c663b0ea966609a97dc9e98e9e5ccbdc8cff`)
- **Primary context:** `docs/features/active/sensitivity-redaction-18/remediation-inputs.2026-07-02T09-45.md`

## Required References

- Remediation inputs (authoritative fix list): `docs/features/active/sensitivity-redaction-18/remediation-inputs.2026-07-02T09-45.md`
- Reviewer coverage evidence: `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review.2026-07-02T09-45.md`
- Cross-language code change policy: `.claude/rules/general-code-change.md`
- Cross-language unit test policy: `.claude/rules/general-unit-test.md`
- C# standards: `.claude/rules/csharp.md` (MSTest + FluentAssertions is the established suite framework; follow the suite per `spec.md` Constraints)
- Quality tiers: `.claude/rules/quality-tiers.md` (`OpenClaw.MailBridge` is T2)
- Original plan (for context; superseded checklist): `docs/features/active/sensitivity-redaction-18/plan.2026-07-02T08-36.md`

**All work must comply with these policies; do not duplicate their content here.**

## Scope Summary — Findings Being Remediated

All changes in this cycle are **test-only**. No production file under `src/` may change. If a production change proves unavoidable it must be raised as a blocked state before proceeding, not implemented silently.

1. **Fix 1 (Blocking)** — `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` branch coverage is 71.43% (10/14), below the uniform 75% gate. Uncovered conditions: the `isMeeting == true` arms of both ternaries at line 63 (`NormalizeSensitiveMessage`), the true short-circuit of `GetOptionalBool(item, "Attachments") || GetOptionalBool(item, "HasAttachments")` at line 63, and the `string.IsNullOrWhiteSpace(messageClass) == true` short-circuit at line 170 (`IsMeetingItem`). Root cause: every sensitive-message test uses a non-meeting `IPM.Note` double. Remedy: new scanner-level tests normalizing a sensitive meeting message (`IPM.Schedule.Meeting.Request`), a null-`MessageClass` item, and an `Attachments == true` variant. Covering all four conditions yields 14/14 branch coverage on line 63/170 conditions (>= 75% gate satisfied with margin).
2. **Fix 2 (Major)** — T2 property-test density for the three new pure functions (`IsSensitive`, `RedactMessage`, `RedactEvent`). **Decision for this cycle: option (b)** — deterministic exhaustive/parameterized invariant tests over the full meaningful `Sensitivity` input domain plus structural redaction invariants and idempotence, instead of introducing CsCheck. **Rationale:** no property-testing library exists anywhere in the repository; adding a new package dependency mid-remediation conflicts with the dependency-minimization policy in `.claude/rules/general-code-change.md` ("Use only libraries already approved in the project unless explicitly told to add more") and repo-wide CsCheck adoption is a deliberate decision that belongs in its own tracked change, not a remediation cycle. The deterministic tests verify the same invariants a property test would (full domain for `IsSensitive`; exact-protected-set transformation, mechanical-field preservation, and idempotence for the two transforms), satisfying the T2 gate's intent pending the repo-wide adoption decision. This decision is recorded as a dated evidence artifact (P2-T1).
3. **Fix 3** — the spec/issue "Toolchain and coverage" AC item (T1) was checked while the new-file branch gate was failing. It is unchecked at cycle start and re-checked with a dated re-affirmation only after the coverage-verification task passes.
4. **Bundled Minor (per remediation-inputs "Optional" section)** — exercise the unused `ThrowOnProtectedAccess` capability in `SensitivityRedactionTestDoubles.cs` with one hard never-ingest test per item kind.

## Do-Not-Do Constraints (binding, from remediation-inputs)

- Do not modify production redaction/shaping logic to chase coverage.
- Do not weaken, remove, or invert any existing assertion; do not relax `mailbridge.runsettings` or add coverage exclusions for production files.
- Do not add sleeps, wall-clock reads, temporary files, or live-COM dependencies (use the existing fake-COM double pattern and fixed-clock seam).
- Do not edit policy documents under `.claude/rules/` or the AC **text** in `spec.md`/`user-story.md`/`issue.md` (checkbox state changes for the T1 re-affirmation are in scope; wording changes are not).
- Do not grow `src/OpenClaw.MailBridge/OutlookScanner.cs` or any file past the 500-line cap.
- No new dependencies (Fix 2 is resolved via option (b); CsCheck is not added this cycle).

## Toolchain Commands (C#)

`csharpier format .` / `csharpier check .` (global CSharpier; no local tool manifest) → `dotnet build OpenClaw.MailBridge.sln` (analyzers + nullable as errors) → `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.

Evidence root (canonical, non-overridable): `docs/features/active/sensitivity-redaction-18/evidence/<kind>/`. Remediation-cycle baselines go under `evidence/remediation-baseline/`; final QA under `evidence/qa-gates/`; the Fix 2 decision record under `evidence/other/`. Raw coverage intermediates (Cobertura XML, TRX) may stage under `artifacts/csharp/`; evidence summaries live only under the canonical evidence root. `<ts>` denotes an ISO-8601 `yyyy-MM-ddTHH-mm` timestamp captured at execution time (distinct from the 2026-07-02T09-25 pre-remediation artifacts).

## Implementation Plan (Atomic Tasks)

### Phase 0 — Remediation Baseline Capture & Policy Compliance

- [x] [P0-T1] Read repo policy files in order: `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/tonality.md`; then read `docs/features/active/sensitivity-redaction-18/remediation-inputs.2026-07-02T09-45.md` and `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-review.2026-07-02T09-45.md`. Write `docs/features/active/sensitivity-redaction-18/evidence/remediation-baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of all files read.
  - Acceptance: artifact exists with all three required fields and lists all seven files.
- [x] [P0-T2] Capture the formatting baseline: run `csharpier check .` from the repo root and write `docs/features/active/sensitivity-redaction-18/evidence/remediation-baseline/csharpier-check.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact exists with all four fields; `Output Summary:` states pass/fail and any unformatted file count.
- [x] [P0-T3] Capture the build/lint/type-check baseline: run `dotnet build OpenClaw.MailBridge.sln` and write `docs/features/active/sensitivity-redaction-18/evidence/remediation-baseline/dotnet-build.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (error/warning counts).
  - Acceptance: artifact exists with all four fields; `EXIT_CODE: 0` expected at head `d267c66`.
- [x] [P0-T4] Capture the test-and-coverage baseline: run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and write `docs/features/active/sensitivity-redaction-18/evidence/remediation-baseline/dotnet-test-coverage.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including test pass/fail counts, **numeric pooled line and branch percentages**, and **per-file line AND branch percentages for `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs`** parsed from the MailBridge Cobertura report (expected to reproduce the finding: line 100.00% (109/109), branch 71.43% (10/14); reviewer reference values: pooled 90.51% line / 79.60% branch).
  - Acceptance: artifact exists with all four fields; `Output Summary:` contains all numeric values (placeholders such as `UNVERIFIED` are invalid); the sub-75% branch figure for `OutlookScanner.Redaction.cs` is confirmed as the remediation starting point.
- [x] [P0-T5] Record baseline line counts for the test files in scope (`tests/OpenClaw.MailBridge.Tests/SensitivityRedactionTestDoubles.cs` — expected 191, `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationTests.cs` — expected 364) in `docs/features/active/sensitivity-redaction-18/evidence/remediation-baseline/file-line-counts.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
  - Acceptance: artifact lists a line count for each file; counts are the not-to-exceed-500 references for P3-T6.
- [x] [P0-T6] Uncheck the "Toolchain and coverage" AC checkbox (change `[x]` to `[ ]`, no wording change) in `docs/features/active/sensitivity-redaction-18/spec.md` (the single item under `### Toolchain and coverage`, line ~220) and in `docs/features/active/sensitivity-redaction-18/issue.md` (the single item under `### Toolchain and coverage`, line ~51), marking the item pending re-verification for remediation cycle 1.
  - Acceptance: both files show the item unchecked; `git diff` for both files shows only the two checkbox-state characters changed.

### Phase 1 — Fix 1 (Blocking): Sensitive Meeting-Message Branch Coverage (test-only)

These tests target correct, already-delivered production behavior; they are expected to pass on first run (no `[expect-fail]` tagging, no fail-before capture — this is a coverage gap, not a behavior defect).

- [x] [P1-T1] Extend `tests/OpenClaw.MailBridge.Tests/SensitivityRedactionTestDoubles.cs` — on `AccessRecordingSensitiveMailItem` only: (a) change `MessageClass` from `string` to `string?` (default `"IPM.Note"` retained); (b) add mechanical init property `public int? MeetingType { get; init; }`; (c) add mechanical init property `public bool Attachments { get; init; }` (default `false`). No change to `AccessRecordingSensitiveAppointmentItem`; no change to protected-member recording. File stays <= 500 lines.
  - Acceptance: `dotnet build OpenClaw.MailBridge.sln` exits 0; `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerSensitivityNormalizationTests"` exits 0 (existing tests unaffected).
- [x] [P1-T2] Create `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationEdgeTests.cs` (MSTest + FluentAssertions, AAA, fixed clock, no temp files, modeled on the scan helpers in `OutlookScannerSensitivityNormalizationTests.cs`) containing a sensitive **meeting-message** test with `[DataRow(2)]`/`[DataRow(3)]`: double configured with `MessageClass = "IPM.Schedule.Meeting.Request"` and `MeetingType = 1`; assert full Group A redaction disposition (`Subject = "Private message"`, the seven protected fields null, `IsRedacted = true`, `ProtectedFieldsAvailable = false`), `ItemKind == "meeting"`, `BridgeId == BridgeIdCodec.MessageId(<entry id>, true)`, retained `MeetingMessageType == 1`, and zero protected-member accesses (`ProtectedMemberWasAccessed == false`). This covers the `isMeeting == true` arms of both line-63 ternaries.
  - Acceptance: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerSensitivityNormalizationEdgeTests"` exits 0 with the new tests passing; file <= 500 lines.
- [x] [P1-T3] Add to `OutlookScannerSensitivityNormalizationEdgeTests.cs` a **null-`MessageClass`** test: double configured with `Sensitivity = 2` and `MessageClass = null` (runtime type name contains no "Meeting"); assert the DTO is fully redacted, `ItemKind == "mail"`, `MessageClass` is null on the DTO, and zero protected-member accesses. This covers the `string.IsNullOrWhiteSpace(messageClass) == true` short-circuit at line 170 of `OutlookScanner.Redaction.cs`.
  - Acceptance: filtered run of the edge-test class exits 0 including the new test.
- [x] [P1-T4] Add to `OutlookScannerSensitivityNormalizationEdgeTests.cs` an **attachments short-circuit** test: double configured with `Sensitivity = 2`, `Attachments = true`, `HasAttachments = false`; assert the redacted DTO has `HasAttachments == true` (true short-circuit of the `||` at line 63) and full redaction disposition holds.
  - Acceptance: filtered run of the edge-test class exits 0 including the new test.
- [x] [P1-T5] Add to `OutlookScannerSensitivityNormalizationEdgeTests.cs` two **hard never-ingest** tests (bundled Minor per remediation-inputs): (a) scan a `Sensitivity = 2` `AccessRecordingSensitiveMailItem` with `ThrowOnProtectedAccess = true` and assert a fully redacted DTO is produced with zero protected accesses; (b) scan a `Sensitivity = 3` `AccessRecordingSensitiveAppointmentItem` with `ThrowOnProtectedAccess = true` and assert a fully redacted `EventDto` is produced with zero protected accesses. This exercises the previously unused `ThrowOnProtectedAccess` capability on both doubles.
  - Acceptance: filtered run of the edge-test class exits 0 including both new tests.
- [x] [P1-T6] Verify the Phase 1 increment cleanly passes the loop: run `csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, and the full `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings`; restart from formatting if any step fails or changes files.
  - Acceptance: all four commands exit 0 in one consecutive pass; no existing test regresses; `git diff --name-only` shows no file under `src/`.

### Phase 2 — Fix 2 (Major): Deterministic Invariant Tests for the Three Pure Functions

- [x] [P2-T1] Write the property-testing decision record to `docs/features/active/sensitivity-redaction-18/evidence/other/property-test-decision.<ts>.md` with `Timestamp:` and the recorded decision: T2 property-test density for `IsSensitive`, `RedactMessage`, and `RedactEvent` is satisfied this cycle by deterministic exhaustive/parameterized invariant tests (option (b) of remediation-inputs Fix 2, as directed by the remediation directive) rather than by introducing CsCheck; rationale: no property-testing library exists repo-wide, adding a new package dependency mid-remediation conflicts with the dependency-minimization policy in `.claude/rules/general-code-change.md`, and repo-wide CsCheck adoption is a deliberate decision to be tracked separately; the artifact names the test file (`tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionInvariantTests.cs`) and enumerates the invariants verified per function.
  - Acceptance: artifact exists with `Timestamp:`, the decision, the rationale, and the per-function invariant list.
- [x] [P2-T2] Create `tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionInvariantTests.cs` (MSTest + FluentAssertions, deterministic, no randomness) with an `IsSensitive` full-domain invariant test: exhaustively assert `OutlookScanner.IsSensitive(n) == (n is 2 or 3)` over the complete Outlook `olSensitivity` domain `0..3`, `null`, and out-of-range boundary representatives (`int.MinValue`, `-1`, `4`, `5`, `99`, `int.MaxValue`), via `[DataTestMethod]`/`[DynamicData]` or an in-test loop over the enumerated domain; include a comment stating the domain-sampling rationale (exhaustive over the defined enum range; boundary-sampled outside it).
  - Acceptance: `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~OutlookScannerRedactionInvariantTests"` exits 0; the test enumerates all eleven listed values.
- [x] [P2-T3] Add to `OutlookScannerRedactionInvariantTests.cs` `RedactMessage` invariant tests over a parameterized `MessageDto` input matrix (variants must include: fully populated protected fields, all-protected-already-null, `ItemKind` "mail" and "meeting", `Sensitivity` 2 and 3, differing mechanical flag combinations): for every variant assert (a) exactly the protected set is transformed (`Subject == "Private message"`; `SenderName`, `SenderEmail`, `SenderEmailResolved`, `FromEmailAddress`, `ToJson`, `CcJson`, `BodyPreview` null; `IsRedacted == true`; `ProtectedFieldsAvailable == false`), (b) every mechanical field (`BridgeId`, `ItemKind`, `MessageClass`, `ReceivedUtc`, `SentUtc`, `Importance`, `Sensitivity`, `Unread`, `HasAttachments`, `ConversationId`, `MeetingMessageType`) equals the input value, and (c) idempotence: `RedactMessage(RedactMessage(x))` is structurally equivalent to `RedactMessage(x)` (assert via `Should().BeEquivalentTo`).
  - Acceptance: filtered run of the invariant-test class exits 0 including the new tests; all three invariants asserted for every matrix variant.
- [x] [P2-T4] Add to `OutlookScannerRedactionInvariantTests.cs` the equivalent `RedactEvent` invariant tests over a parameterized `EventDto` input matrix (populated vs already-null protected fields; `Sensitivity` 2 and 3; populated `Categories` vs empty): for every variant assert (a) exactly the protected set is transformed (`Subject == "Private appointment"`; `Location`, `Organizer`, `RequiredAttendeesJson`, `OptionalAttendeesJson`, `ResourcesJson`, `BodyPreview`, `BodyFull` null; `Categories` empty **and non-null**; `IsRedacted == true`; `ProtectedFieldsAvailable == false`), (b) every mechanical field (`BridgeId`, `GlobalAppointmentId`, `StartUtc`, `EndUtc`, `BusyStatus`, `MeetingStatus`, `IsRecurring`, `Sensitivity`, `SensitivityLabel`, `ResponseStatus`, `IsOrganizer`, `IsOnlineMeeting`, `AllowNewTimeProposals`, `ICalUId`, `SeriesMasterId`, `LastModifiedDateTime`) equals the input value, and (c) idempotence via `Should().BeEquivalentTo`. File <= 500 lines (split into a second invariant-test file if needed).
  - Acceptance: filtered run of the invariant-test class exits 0 including the new tests; the `Categories` non-null assertion is present.
- [x] [P2-T5] Verify the Phase 2 increment cleanly passes the loop: run `csharpier format .`, `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, and the full `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings`; restart from formatting if any step fails or changes files.
  - Acceptance: all four commands exit 0 in one consecutive pass; `git diff --name-only` still shows no file under `src/`.

### Phase 3 — Final QA Loop, Coverage Verification & AC Re-affirmation

Loop rule (non-skippable): if any of P3-T1 through P3-T3 fails or modifies files, remediate and restart from P3-T1; all recorded artifacts must come from the final consecutive clean pass. `EXIT_CODE: SKIPPED` is invalid for every task in this phase.

- [x] [P3-T1] Run `csharpier format .` then `csharpier check .` from the repo root; write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-format.<ts>.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` for the check command.
  - Acceptance: artifact exists; `EXIT_CODE: 0`; `Output Summary:` confirms zero unformatted files.
- [x] [P3-T2] Run `dotnet build OpenClaw.MailBridge.sln`; write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-build.<ts>.md` with the four schema fields.
  - Acceptance: artifact exists; `EXIT_CODE: 0`; zero warnings/errors reported in `Output Summary:`.
- [x] [P3-T3] Run `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`; write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-test-coverage.<ts>.md` with the four schema fields, including test counts and **numeric post-change pooled line and branch coverage percentages** (raw Cobertura/TRX may stage under `artifacts/csharp/`).
  - Acceptance: artifact exists; `EXIT_CODE: 0`; numeric pooled line % and branch % recorded (placeholders invalid).
- [x] [P3-T4] Produce the remediation coverage verification: parse the P3-T3 MailBridge Cobertura report and write `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/coverage-remediation-verification.<ts>.md` reporting (a) per-file **line AND branch** percentages with covered/total counts for `src/OpenClaw.MailBridge/OutlookScanner.Redaction.cs` — branch MUST be >= 75% (expected 14/14 = 100.00% given P1-T2/T3/T4; 13/14 = 92.86% also passes) — and for the other three changed files (`OutlookScanner.cs`, `OutlookScanner.GraphFields.cs`, `ResponseShaper.cs`); (b) pooled line/branch versus the P0-T4 remediation baseline and versus the pre-remediation reference (90.51% line / 79.60% branch) showing no pooled regression; (c) an explicit PASS/FAIL verdict per threshold (line >= 85%, branch >= 75%, per-file branch gate on the new file, no pooled regression).
  - Acceptance: artifact contains all numeric groups with covered/total counts and per-threshold verdicts; if any required value is unavailable or any verdict is FAIL, the plan outcome is remediation-required, never PASS.
- [x] [P3-T5] Confirm the final pass was a single consecutive clean pass: verify the P3-T1/T2/T3 artifacts carry `EXIT_CODE: 0` from the same final iteration and that no file changed between P3-T1's format run and P3-T3's test run (`git status --porcelain` stable across the pass); record the confirmation in `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/final-single-pass.<ts>.md` citing the three sibling artifacts by filename.
  - Acceptance: artifact exists and cites the three sibling artifacts with matching iteration timestamps.
- [x] [P3-T6] Verify file-size caps and the test-only constraint: (a) confirm every touched file (`SensitivityRedactionTestDoubles.cs`, `OutlookScannerSensitivityNormalizationEdgeTests.cs`, `OutlookScannerRedactionInvariantTests.cs`, plus any split file) is <= 500 lines; (b) run `git diff --name-only d267c663b0ea966609a97dc9e98e9e5ccbdc8cff..HEAD` and confirm no path under `src/` appears (only `tests/` and `docs/features/active/sensitivity-redaction-18/` paths). Write counts and the diff file list to `docs/features/active/sensitivity-redaction-18/evidence/qa-gates/file-size-and-diff-scope-check.<ts>.md` with the four schema fields.
  - Acceptance: artifact lists each touched file with its line count, all <= 500; the diff list contains no `src/` path.
- [x] [P3-T7] Re-affirm the "Toolchain and coverage" AC item (Fix 3): only after P3-T4 records a PASS verdict on every threshold, re-check the checkbox (change `[ ]` to `[x]`) in `docs/features/active/sensitivity-redaction-18/spec.md` and `docs/features/active/sensitivity-redaction-18/issue.md`, and append beneath the spec.md item a dated re-verification sub-bullet (no wording change to the criterion itself): `  - Re-verified <ts> (remediation cycle 1): OutlookScanner.Redaction.cs branch coverage >= 75% per evidence/qa-gates/coverage-remediation-verification.<ts>.md.`
  - Acceptance: both checkboxes are re-checked; the spec.md sub-bullet cites the P3-T4 artifact by path; the criterion text above the sub-bullet is byte-identical to its pre-remediation wording.

## Test Plan

- **Unit (scanner integration, fake COM) — Fix 1:** `tests/OpenClaw.MailBridge.Tests/OutlookScannerSensitivityNormalizationEdgeTests.cs` — sensitive meeting-message normalization (`IPM.Schedule.Meeting.Request`, Sensitivity 2/3), null-`MessageClass` fallback, `Attachments` short-circuit, hard never-ingest (`ThrowOnProtectedAccess`) per item kind. Extends `SensitivityRedactionTestDoubles.cs` with `MeetingType`, `Attachments`, and nullable `MessageClass`. No live COM, no temp files, fixed clock.
- **Unit (pure transforms, deterministic invariants) — Fix 2:** `tests/OpenClaw.MailBridge.Tests/OutlookScannerRedactionInvariantTests.cs` — `IsSensitive` full-domain equivalence, `RedactMessage`/`RedactEvent` exact-protected-set + mechanical-preservation + idempotence matrices. Decision record: `evidence/other/property-test-decision.<ts>.md`.
- **Existing suite:** all 652 tests (647 passed / 5 environment-gated skips at head) must continue to pass; no existing assertion is weakened, removed, or inverted.
- **Coverage evidence:** remediation baseline `evidence/remediation-baseline/dotnet-test-coverage.<ts>.md` (per-file branch 71.43% reproduced); post-change `evidence/qa-gates/final-test-coverage.<ts>.md`; verification `evidence/qa-gates/coverage-remediation-verification.<ts>.md` (per-file line AND branch for `OutlookScanner.Redaction.cs`, pooled no-regression).

## Finding Traceability

| Finding | Severity | Delivering tasks | Verifying tasks/artifacts |
|---|---|---|---|
| 1 — `OutlookScanner.Redaction.cs` branch 71.43% < 75% | Blocking | P1-T1, P1-T2, P1-T3, P1-T4 | P0-T4 (reproduces 71.43%), P3-T3, P3-T4 (branch >= 75% with per-file line AND branch) |
| 2 — T2 property-test density for `IsSensitive`/`RedactMessage`/`RedactEvent` | Major | P2-T2, P2-T3, P2-T4 | P2-T1 (decision record), P2-T5, P3-T3 |
| 3 — T1 AC checked while gate failing | Process | P0-T6 (uncheck) | P3-T7 (recheck with dated annotation citing P3-T4) |
| Optional Minor — unused `ThrowOnProtectedAccess` | Minor (bundled) | P1-T5 | P1-T6, P3-T3 |

## Open Questions / Notes

- Expected branch outcome: covering the two line-63 ternary true-arms (P1-T2), the line-170 short-circuit (P1-T3), and the `||` true short-circuit (P1-T4) takes the new file from 10/14 to 14/14 branch conditions; the gate requires only >= 75% (11/14), so the plan carries margin.
- No `[expect-fail]` tasks: all new tests assert already-correct delivered behavior; there is no failing-run requirement for a coverage-gap remediation. The pre-existing fail-before dossiers under `evidence/regression-testing/` from the original cycle remain the fail-before record for the feature.
- The `MessageClass` nullability change on the mail double (`string` → `string?`) is test-infrastructure only; the default `"IPM.Note"` is retained so all existing tests compile and pass unchanged.
- Idempotence assertions use `Should().BeEquivalentTo` (structural) rather than record `==`, because `EventDto.Categories` is an array member whose default record equality is referential.
- Evidence location invariant: all evidence under `docs/features/active/sensitivity-redaction-18/evidence/<kind>/` (`remediation-baseline`, `qa-gates`, `other`); `artifacts/csharp/` is staging for raw coverage intermediates only, never for evidence summaries.
- Plan-path continuity: this file (`remediation-plan.2026-07-02T09-45.md`) is the single plan file for remediation cycle 1; all preflight revision iterations update it in place.
