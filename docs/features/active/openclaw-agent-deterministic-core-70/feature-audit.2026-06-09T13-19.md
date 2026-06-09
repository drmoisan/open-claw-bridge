# Feature Audit: openclaw-agent-deterministic-core (#70)

**Audit Date:** 2026-06-09
**Feature Folder:** `docs/features/active/openclaw-agent-deterministic-core-70`
**Base Branch:** `main`
**Head Branch:** `open-claw-bridge-wt-2026-06-09-11-54`
**Work Mode:** `full-feature`
**Audit Type:** Initial acceptance review

---

## Scope and Baseline

- **Base branch:** `main` (commit `848e326dfdbbb2b533eea290234078aa022cd811`)
- **Head branch/commit:** `open-claw-bridge-wt-2026-06-09-11-54` (commit `f51468a9b6d652ea71aabf0253f64c10d6d5aaab`)
- **Merge base:** `848e326dfdbbb2b533eea290234078aa022cd811`
- **Evidence sources:**
  - Primary: `artifacts/pr_context.summary.txt`
  - Secondary baseline diff: `artifacts/pr_context.appendix.txt`
  - Feature evidence: `docs/features/active/openclaw-agent-deterministic-core-70/evidence/**`
  - Additional evidence: reviewer-rerun build/test + reviewer cobertura `tests/OpenClaw.Core.Tests/TestResults/a87c7d06-924b-483c-bbd6-6af45ff6d8c7/coverage.cobertura.xml`; direct source inspection of `src/OpenClaw.Core/Agent/**`
- **Feature folder used:** `docs/features/active/openclaw-agent-deterministic-core-70`
- **Requirements source:** `spec.md` (AC-1..AC-13) and `user-story.md` (AC-U1..AC-U4)
- **Work mode resolution note:** `issue.md` is absent from the feature folder. Per the work-mode contract, a missing/malformed marker fails closed to `full-feature`, which the caller also specified explicitly. Authoritative AC sources are therefore `spec.md` and `user-story.md`.
- **Scope note:** Scope is the full branch diff against the merge-base. The PR-context summary's "Changed files overview" mis-categorized the change as 0 core-logic files; the authoritative `git diff 848e326..f51468a` shows 51 production C# files plus tests, config, and docs. The audit used the authoritative diff.

---

## Acceptance Criteria Inventory

**Authoritative AC source files for this run:**
- `spec.md` — primary (AC-1..AC-13)
- `user-story.md` — primary (AC-U1..AC-U4)

### From spec.md

1. AC-1 (D1): `MeetingContextNormalizer.Normalize(mailboxUpn, message, event)` is a pure function producing a `NormalizedMeetingContext` with trimmed-lowercase emails, HTML-stripped body, required/optional/resource attendee partitioning per master 9.2, including the no-event fallback to message subject/body.
2. AC-2 (D2): `TriageEngine.Triage(context, policy)` returns exactly one of IGNORE, PRIVATE_BUSY_ONLY, PROTECTED_MEETING, HUMAN_APPROVAL, AUTO_COORDINATE for every valid input, and `DependencyScorer.Score` applies master 9.2 weights/thresholds.
3. AC-3 (D3): `OwnerPriorityClassifier.Classify` returns a valid `OwnerPriority`; `RecurringMeetingClassifier.Classify` returns one of the four recurrence kinds; `MovePolicy.CanMove` returns false for a RECURRING_FORUM when the requester is neither owner nor meeting owner.
4. AC-4 (D3): priority/move layers run only after AUTO_COORDINATE/HUMAN_APPROVAL; PROTECTED_MEETING stays protected and PRIVATE_BUSY_ONLY stays opaque regardless of priority.
5. AC-5 (D4): `SlotProposer.ProposeTimes` is deterministic given an injected `TimeProvider` and returns an ordered `CandidateSlot` list within working hours, outside no-meeting blocks, free per free/busy, at least `MinNoticeMinutes` from now, normalized to the mailbox time zone per master 10.4.
6. AC-6 (D5): `AgentPolicyOptions` binds from `appsettings.json` `OpenClaw:AgentPolicy`, exposes all policy lists plus `SendEnabled`/`CalendarWriteEnabled`; both default to false.
7. AC-7 (kill switches): worker does not call `SendMailAsync` when `SendEnabled` is false; performs no calendar write when `CalendarWriteEnabled` is false; the pipeline still computes/logs its result.
8. AC-8 (D6 seam): `ISchedulingService` and its DTOs exist as the Graph-shaped contract; DTO round-trips produce identical field values.
9. AC-9 (D6 adapter + worker): a HostAdapter-backed `ISchedulingService` mapper wraps `IHostAdapterClient`, mapping `MessageDto`/`EventDto` to the Graph-shaped DTOs (null handling, attendee JSON parsing), and a `SchedulingWorker` orchestrates poll → hydrate → triage → priority → propose.
10. AC-10 (contract-parity invariant): D1–D4 contain no reference to MailBridge/HostAdapter/COM; swapping `ISchedulingService` requires zero D1–D4 change.
11. AC-11 (namespace + tier): agent components reside in `OpenClaw.Core.Agent` within the T1 `OpenClaw.Core` project; the invariant is enforced by a namespace-scoped architecture test.
12. AC-12 (test obligations): at least one property-based test per pure function (`Normalize`, `DependencyScorer.Score`, `TriageEngine.Triage`, `OwnerPriorityClassifier.Classify`, `RecurringMeetingClassifier.Classify`, `MovePolicy.CanMove`, `SlotProposer.ProposeTimes`) using CsCheck/FsCheck, with line >= 85% / branch >= 75% on agent code; async/time tests use `FakeTimeProvider`.
13. AC-13 (dependencies deferred): spec/code reference #71–#76 as deferred work; agent logic is complete against partial data and implements none of #71–#76.

### From user-story.md

- AC-U1: deterministic logic consumes data only through the `ISchedulingService` seam and its DTOs, with no MailBridge/HostAdapter/COM reference; swapping the implementation requires zero change.
- AC-U2: inbound traffic classified into exactly one of five decision classes; private meetings busy-only (no semantic ingestion); protected meetings never auto-coordinated regardless of priority.
- AC-U3: operator configures all policy lists and the two kill switches in one config model bound from `appsettings.json`; both default off; no outbound mail/calendar write while off.
- AC-U4: for AUTO_COORDINATE/HUMAN_APPROVAL items needing scheduling, the agent proposes deterministic reproducible slots within working hours, avoiding no-meeting blocks, free, respecting minimum notice, ordered by day preference then start time.

---

## Acceptance Criteria Evaluation

| # | Criterion | Status | Evidence | Verification command(s) | Notes |
|---|-----------|--------|----------|--------------------------|-------|
| AC-1 | D1 pure normalization | PASS | `src/OpenClaw.Core/Agent/MeetingContextNormalizer*.cs`; `MeetingContextNormalizerTests.cs` (300 lines) + `MeetingContextNormalizerPropertyTests.cs`; cobertura L=1. | `dotnet test ... --filter MeetingContextNormalizer` | Pure static; null-guarded; no-event fallback tested. |
| AC-2 | D2 triage + scoring | PASS | `TriageEngine.cs`, `DependencyScorer.cs`, `TriageDecision.cs`; `TriageEngineTests.cs`, `DependencyScorerTests.cs`, `TriagePropertyTests.cs`; cobertura L=1. | `dotnet test ... --filter Triage` | Five decision classes and thresholds verified. |
| AC-3 | D3 classifiers + move policy | PASS | `OwnerPriorityClassifier.cs`, `RecurringMeetingClassifier.cs`, `MovePolicy.cs`; respective test files; cobertura L=1. | `dotnet test ... --filter MovePolicy` | `MovePolicy.CanMove` forum-denial verified in `MovePolicyTests`. |
| AC-4 | D3 layering invariant | PASS | `PriorityLayeringTests.cs`; triage gates priority/move; PROTECTED/PRIVATE remain regardless of priority. | `dotnet test ... --filter PriorityLayering` | Layering ordering asserted. |
| AC-5 | D4 deterministic slot proposal | PASS | `SlotProposer.cs`, `SlotProposer.Window.cs`, `WorkingHoursPolicy.cs`; `SlotProposerTests.cs`, `SlotProposerPropertyTests.cs` with `FakeTimeProvider`. | `dotnet test ... --filter SlotProposer` | Ordering and min-notice verified; window file L=0.8875/B=0.7187 (defensive midnight guard), above package gate. |
| AC-6 | D5 options + kill-switch defaults | PASS | `Agent/Contracts/AgentPolicyOptions.cs` (`SendEnabled`/`CalendarWriteEnabled` auto-props, no initializer → false); `appsettings.json` `OpenClaw:AgentPolicy`. | `grep -n 'SendEnabled\|CalendarWriteEnabled' AgentPolicyOptions.cs` | Both defaults confirmed false by inspection. |
| AC-7 | Kill-switch gating | PASS | `Runtime/SchedulingWorker.cs`/`.Pipeline.cs`; `Runtime/SchedulingWorkerTests.cs` asserts no `SendMailAsync`/no calendar write when off, pipeline still computes. | `dotnet test ... --filter SchedulingWorker` | Verified by worker tests. |
| AC-8 | D6 seam + DTO round-trip | PASS | `Agent/Contracts/ISchedulingService.cs` + 6 DTO records; `SchedulingDtoContractTests.cs` round-trip equality. | `dotnet test ... --filter SchedulingDtoContract` | Seven members match spec; round-trip tested. |
| AC-9 | D6 adapter + worker | PASS | `Runtime/HostAdapterSchedulingService.cs`, `Runtime/SchedulingDtoMapper.cs`; `HostAdapterSchedulingServiceTests.cs`, `SchedulingDtoMapperTests.cs`. | `dotnet test ... --filter SchedulingDtoMapper` | Attendee JSON parsing and null handling tested; mapper branch 78.78% (deferred-field defensive paths). |
| AC-10 | Contract-parity invariant | PASS | `grep` over `src/OpenClaw.Core/Agent` (excl. Runtime) shows no MailBridge/HostAdapter/COM use (only XML-doc mentions); `AgentArchitectureBoundaryTests` passes. | `dotnet test ... --filter AgentArchitectureBoundaryTests` | Enforced by NetArchTest + project graph. |
| AC-11 | Namespace + tier | PASS | All agent code in `OpenClaw.Core.Agent` within `OpenClaw.Core` (T1 in `quality-tiers.yml`); no new project/ProjectReference. | `grep 'OpenClaw.Core' quality-tiers.yml` | `OpenClaw.Core: T1` confirmed; csproj references only `HostAdapter.Contracts`. |
| AC-12 | Test obligations (property density + coverage) | PARTIAL | Coverage 98.57% line / 90.32% branch (PASS); `FakeTimeProvider` used (PASS); property tests present for 6 of 7 pure functions. `RecurringMeetingClassifier.Classify` has only example tests, no CsCheck property test. | `grep 'Classify' tests/.../*PropertyTests.cs` | Coverage and determinism met; T1 property-density gate unmet for one function. Non-blocking; behavior fully covered by example tests (class L=1/B=1). |
| AC-13 | Dependencies deferred | PASS | `spec.md` Non-Goals/Risks and issue #70 comment reference #71–#76; no #71–#76 implementation in the diff. | diff inspection | Agent operates on partial data by design. |
| AC-U1 | Seam-only consumption, zero-change swap | PASS | Same evidence as AC-10; `AgentArchitectureBoundaryTests` passes. | `dotnet test ... --filter AgentArchitectureBoundaryTests` | Mirrors AC-10. |
| AC-U2 | Five decision classes, private/protected handling | PASS | `TriageEngineTests` cover all five classes; PRIVATE_BUSY_ONLY returns without ingesting semantics; PROTECTED never auto-coordinated. | `dotnet test ... --filter Triage` | Mirrors AC-2/AC-4. |
| AC-U3 | Single config model, kill switches default off | PASS | `AgentPolicyOptions.cs` + `appsettings.json`; defaults false; worker gates outbound effects. | `grep` defaults; `SchedulingWorkerTests` | Mirrors AC-6/AC-7. |
| AC-U4 | Deterministic reproducible slot proposal | PASS | `SlotProposer*` with `FakeTimeProvider`; ordering/working-hours/no-meeting-block/free/min-notice asserted. | `dotnet test ... --filter SlotProposer` | Mirrors AC-5. |

---

## Summary

**Overall Feature Readiness:** NEEDS REVISION (minor, non-blocking)

**Criteria summary:**
- **PASS:** 16 criteria (AC-1..AC-11, AC-13, AC-U1..AC-U4)
- **PARTIAL:** 1 criterion (AC-12)
- **UNVERIFIED:** 0 criteria
- **FAIL:** 0 criteria

**Top gaps preventing full PASS:**

1. AC-12: `RecurringMeetingClassifier.Classify` lacks a CsCheck property-based test, leaving the T1 property-test density gate unmet for one of seven pure functions. The function is fully covered by deterministic example tests (100% line/branch), so the gap is the property test specifically, not behavioral verification.

**Recommended follow-up verification steps:**

1. Add one CsCheck property test for `RecurringMeetingClassifier.Classify` (assert it always returns a defined `RecurringMeetingKind` and that the recurrence partition invariants hold across generated inputs), then re-run `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --filter RecurringMeeting`.
2. Re-run the strict build and coverage to confirm no regression after adding the test.

---

## Acceptance Criteria Check-off

Per the acceptance-criteria tracking rules:
- PASS criteria represented as checkboxes are checked off if not already checked.
- PARTIAL/FAIL/UNVERIFIED criteria remain unchecked.

All 13 `spec.md` and all 4 `user-story.md` criteria were already marked `[x]` by the executor prior to this review. The 16 criteria evaluated PASS remain correctly checked; no new check-off mutation was required. AC-12 was evaluated PARTIAL by this review; it is already `[x]` in `spec.md`. This reviewer did not alter its checkbox state, but records the PARTIAL disposition here and in `remediation-inputs.2026-06-09T13-19.md`: the executor's `[x]` reflects the coverage/determinism portions of AC-12 being met, while the property-density sub-obligation for `RecurringMeetingClassifier.Classify` is outstanding. No source-file checkbox was unchecked, because the tracking rules direct reviewers to check off PASS items and leave non-PASS items unchecked, not to uncheck items already set by an executor; the gap is captured in remediation inputs instead.

### AC Status Summary

- Source: `spec.md` (AC-1..AC-13), `user-story.md` (AC-U1..AC-U4)
- Total AC items: 17
- Checked off (delivered): 17 (pre-existing executor check-offs)
- Remaining (unchecked): 0
- Items remaining: None unchecked; AC-12 carries an outstanding PARTIAL sub-obligation tracked in remediation inputs despite its checkbox being set.

| Source File | Total AC | Checked (PASS) | Unchecked | Notes |
|-------------|----------|----------------|-----------|-------|
| `spec.md` | 13 | 13 | 0 | Checkbox-backed. AC-12 evaluated PARTIAL by review; checkbox left as set by executor, gap tracked in remediation inputs. |
| `user-story.md` | 4 | 4 | 0 | Checkbox-backed; all evaluated PASS. |

No source-file checkbox change was made by this review: all PASS items were already checked, and the tracking rules do not direct reviewers to uncheck an executor-set item, so the AC-12 PARTIAL is recorded in this audit and in remediation inputs.
