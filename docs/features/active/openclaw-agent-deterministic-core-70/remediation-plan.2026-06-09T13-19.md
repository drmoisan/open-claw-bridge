# Remediation Plan: openclaw-agent-deterministic-core (#70)

**Entry Timestamp:** 2026-06-09T13-19
**Feature Folder:** `docs/features/active/openclaw-agent-deterministic-core-70`
**Plan File:** `docs/features/active/openclaw-agent-deterministic-core-70/remediation-plan.2026-06-09T13-19.md`
**Inputs:** `docs/features/active/openclaw-agent-deterministic-core-70/remediation-inputs.2026-06-09T13-19.md`
**Scope:** Single-fix, test-only remediation (FIX-1) against one non-blocking material PARTIAL finding (AC-12 / T1 property-test density).
**Base Branch:** `main` (merge-base `848e326dfdbbb2b533eea290234078aa022cd811`)
**Head Branch:** `open-claw-bridge-wt-2026-06-09-11-54`

## Constraints (from Do-Not-Do list — binding)

- Test-only. No production code changes under `src/**`.
- No edits to policy documents under `.claude/rules/**` or `.github/instructions/**`.
- No new dependencies (CsCheck is already referenced by `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj`).
- No weakening, deleting, or relaxing of any existing test or assertion.
- No `OpenClaw.Core.Agent` namespace or architecture-boundary changes.
- No implementation of deferred upstream issues #71–#76.
- All evidence written only to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/<kind>/`.

## Target of FIX-1

Pure function under test: `RecurringMeetingClassifier.Classify(NormalizedMeetingContext ctx, string ownerEmail)`
Source: `src/OpenClaw.Core/Agent/RecurringMeetingClassifier.cs` (lines 24–51).
Enum under assertion: `RecurringMeetingKind` — members `NON_RECURRING`, `ONE_ON_ONE`, `RECURRING_FORUM`, `RECURRING_OTHER` (`src/OpenClaw.Core/Agent/RecurringMeetingKind.cs`).
New test file: `tests/OpenClaw.Core.Tests/Agent/RecurringMeetingClassifierPropertyTests.cs`.
Pattern references (existing `*PropertyTests.cs` in the same folder): `TriagePropertyTests.cs` (CsCheck `Gen.Select` context generator, `.Sample(..., iter: 1000)`), `SlotProposerPropertyTests.cs` (seeded determinism with `FakeTimeProvider`).

### Partition invariants to assert (from source behavior, `RecurringMeetingClassifier.cs`)

- `!ctx.IsRecurring` → `NON_RECURRING` (line 29–32).
- recurring AND exactly one non-organizer attendee equal to the normalized owner → `ONE_ON_ONE` (line 36–43).
- recurring AND `ctx.AllAttendees.Count > 5` → `RECURRING_FORUM` (line 45–48).
- any other recurring → `RECURRING_OTHER` (line 50).
- always returns a defined `RecurringMeetingKind` member (`Enum.IsDefined` over `Enum.GetValues<RecurringMeetingKind>()`).

---

### Phase 0 — Policy Reads and Baseline Capture

- [x] [P0-T1] Read the required policy documents in the order defined by `policy-compliance-order`: `CLAUDE.md`, `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/quality-tiers.md`, `.claude/rules/architecture-boundaries.md`. Record a Phase 0 instructions-read artifact at `docs/features/active/openclaw-agent-deterministic-core-70/evidence/baseline/phase0-instructions-read.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of files read. **Verify:** artifact exists and lists all six files with the policy order. **Acceptance:** binary — artifact present with all required fields.

- [x] [P0-T2] Capture baseline formatting state by running CSharpier in check mode. **Command:** `csharpier check .` (run from repo root). Record output to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/baseline/baseline-format.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. **Acceptance:** binary — artifact present with all four fields and the exact exit code.

- [x] [P0-T3] Capture baseline strict build (analyzers + code-style + warnings-as-errors). **Command:** `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true`. Record output to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/baseline/baseline-build.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warning/error counts). **Acceptance:** binary — artifact present with all four fields.

- [x] [P0-T4] Capture baseline test + coverage for `OpenClaw.Core.Tests` with coverage collection. **Command:** `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Record output to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/baseline/baseline-test-coverage.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` that MUST include numeric baseline `OpenClaw.Core` line-coverage percent and branch-coverage percent (expected ~98.57% line / ~90.32% branch per the feature-audit) and the passed/failed test counts. **Acceptance:** binary — artifact present with numeric line and branch coverage values (not placeholders) and pass/fail counts.

### Phase 1 — Add FIX-1 Property Test

- [x] [P1-T1] Create the new property-test file `tests/OpenClaw.Core.Tests/Agent/RecurringMeetingClassifierPropertyTests.cs` in namespace `OpenClaw.Core.Tests.Agent` with a `[TestClass] public sealed class RecurringMeetingClassifierPropertyTests`, modeled on `tests/OpenClaw.Core.Tests/Agent/TriagePropertyTests.cs`. The file MUST define a seedable CsCheck `Gen<NormalizedMeetingContext>` that varies `IsRecurring`, `Organizer`, an attendee set that sometimes includes only the owner as the single non-organizer attendee and sometimes spans the `>5` total-attendee boundary, plus a non-null `ownerEmail`, and MUST use `Gen.Sample(..., iter: 1000)` (CsCheck prints the failing seed automatically on `Sample` failure, satisfying the determinism print-seed requirement). Do not modify any other file. **Verify:** `Test-Path tests/OpenClaw.Core.Tests/Agent/RecurringMeetingClassifierPropertyTests.cs`. **Acceptance:** binary — file exists with the named class in the correct namespace and a seeded CsCheck generator.

- [x] [P1-T2] Add the property method `Classify_AlwaysReturnsDefinedKind` to the new file asserting that for every generated `(ctx, ownerEmail)`, `RecurringMeetingClassifier.Classify(ctx, ownerEmail)` is a member of `Enum.GetValues<RecurringMeetingKind>()` (use `Enum.IsDefined` or `.Should().BeOneOf(...)`/`AllKinds.Should().Contain(result)`). Source behavior: `src/OpenClaw.Core/Agent/RecurringMeetingClassifier.cs` lines 24–51. **Verify:** the method exists in the new file. **Acceptance:** binary — method present asserting a defined enum member over generated inputs.

- [x] [P1-T3] Add the property method `Classify_PartitionInvariants_Hold` to the new file asserting all four partition invariants against generated inputs: (a) `!ctx.IsRecurring` ⇒ `NON_RECURRING`; (b) recurring with exactly one non-organizer attendee equal to the normalized owner ⇒ `ONE_ON_ONE`; (c) recurring with `ctx.AllAttendees.Count > 5` ⇒ `RECURRING_FORUM`; (d) any other recurring ⇒ `RECURRING_OTHER`. The assertion logic MUST mirror the source partition order in `RecurringMeetingClassifier.cs` (ONE_ON_ONE checked before the forum count) and MUST normalize the owner via the same rule the source uses (`MeetingContextNormalizer.NormalizeEmail`). Do not weaken any existing assertion in `RecurringMeetingClassifierTests.cs`. **Verify:** the method exists in the new file. **Acceptance:** binary — method present asserting all four invariants consistent with source order.

- [x] [P1-T4] Run the mandatory toolchain loop until a single clean pass, in order, restarting from formatting if any stage fails or modifies files. (1) Format: `csharpier check .`; if it reports changes, run `csharpier format .` and restart. (2) Lint/type: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true`. (3) Architecture test: `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests" --settings mailbridge.runsettings`. (4) Targeted test: `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~RecurringMeeting" --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Record the final clean pass for each stage to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/qa-gates/phase1-toolchain.2026-06-09T13-19.md` with per-stage `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. **Acceptance:** binary — all four stages recorded with `EXIT_CODE: 0` in a single pass, and the new `RecurringMeetingClassifierPropertyTests` methods appear as passed in the test summary.

### Phase 2 — Final QA Loop, Coverage Delta, and Per-AC Re-Verification

- [x] [P2-T1] Run the full final-QA formatting gate. **Command:** `csharpier check .`. Record to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/qa-gates/final-format.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. **Acceptance:** binary — `EXIT_CODE: 0` recorded.

- [x] [P2-T2] Run the full final-QA strict build (lint + type + analyzers). **Command:** `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true`. Record to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/qa-gates/final-build.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (0 warnings, 0 errors expected). **Acceptance:** binary — `EXIT_CODE: 0` with zero warnings/errors recorded.

- [x] [P2-T3] Run the full final-QA architecture-boundary test. **Command:** `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests" --settings mailbridge.runsettings`. Record to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/qa-gates/final-architecture.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. **Acceptance:** binary — `EXIT_CODE: 0` recorded.

- [x] [P2-T4] Run the full `OpenClaw.Core.Tests` suite with coverage. **Command:** `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Record to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/qa-gates/final-test-coverage.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` that MUST include numeric post-change `OpenClaw.Core` line and branch coverage percentages and passed/failed counts. **Acceptance:** binary — artifact present with numeric post-change line and branch coverage values and full-suite pass.

- [x] [P2-T5] Record the coverage delta and threshold verification at `docs/features/active/openclaw-agent-deterministic-core-70/evidence/qa-gates/coverage-delta.2026-06-09T13-19.md`, reporting: baseline line/branch coverage (from P0-T4), post-change line/branch coverage (from P2-T4), and changed-code coverage for the new test file. Confirm `OpenClaw.Core` line coverage >= 85% and branch coverage >= 75% with no regression versus baseline on changed lines (the change is test-only; production `Classify` coverage must not drop). **Verify:** the artifact reports both baseline and post-change numbers and a PASS/FAIL threshold verdict. **Acceptance:** binary — artifact shows line >= 85%, branch >= 75%, and no regression; otherwise outcome is remediation-required (not PASS).

- [x] [P2-T6] Re-verify the AC-12 property-test density gate: confirm that each of the seven AC-12 pure functions has at least one CsCheck property test. **Command:** `rg -l "\.Sample\(" tests/OpenClaw.Core.Tests/Agent/*PropertyTests.cs` plus `rg -n "Normalize|DependencyScorer.Score|TriageEngine.Triage|OwnerPriorityClassifier.Classify|RecurringMeetingClassifier.Classify|MovePolicy.CanMove|SlotProposer.ProposeTimes" tests/OpenClaw.Core.Tests/Agent/*PropertyTests.cs`. Record the per-function mapping (function → covering `*PropertyTests.cs` file/method) to `docs/features/active/openclaw-agent-deterministic-core-70/evidence/qa-gates/ac12-property-density.2026-06-09T13-19.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. **Acceptance:** binary — all seven AC-12 pure functions (`Normalize`, `DependencyScorer.Score`, `TriageEngine.Triage`, `OwnerPriorityClassifier.Classify`, `RecurringMeetingClassifier.Classify`, `MovePolicy.CanMove`, `SlotProposer.ProposeTimes`) are each mapped to at least one property test, including the new `RecurringMeetingClassifier.Classify` mapping.
