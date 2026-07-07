# Feature Audit — attendee-propose-new-time (Issue #130)

- **Feature folder:** `docs/features/active/2026-07-07-attendee-propose-new-time-130/`
- **Reviewed:** 2026-07-07T06-35 UTC

## Scope and Baseline

- **Branch:** `feature/attendee-propose-new-time-130`, HEAD `1633a6c5645d7ba3905bf134c06f78b2c8cc169e`
  (1 commit ahead of base, 0 behind).
- **Resolved base branch:** `epic/openclaw-vision-integration` (epic-child review; base is the
  epic integration branch, not `main`).
- **Merge-base:** `273c7df25d3a5a0cd928a47e8c80fced592ce06b` (2026-07-07T05:07:21-04:00).
- **Work mode:** `full-feature` (persisted marker in `issue.md`). Acceptance-criteria sources per
  the work-mode contract: `spec.md` **and** `user-story.md`.
- **Diff scope:** 37 files (+3824/-0): 9 production, 6 test (all new), 22
  docs/evidence/runbook/memory. Full enumeration in `policy-audit.2026-07-07T06-35.md` section 1.
- **Evidence basis:** the executor's committed evidence under `evidence/` (cross-checked), the
  executor's final settings-mode Cobertura under `artifacts/csharp/final-2026-07-07T05-56/`
  (parsed per-file in this review), a fresh plain-mode `dotnet test` coverage run in this review
  (`evidence/qa-gates/coverage-review/plain-mode/`), direct source/test reading, and a fresh
  CSharpier check. The PR-context summary's file categorization was not used for scope (it
  misclassified this C# branch as docs-only — see policy audit section 2).

## Acceptance Criteria Inventory

Both source files carry the identical nine checkbox criteria AC-1..AC-9 under
`## Acceptance Criteria`; all 18 checkboxes (9 per file) arrived at review already checked `[x]`
by the executor. Inventory (abbreviated titles):

| # | Criterion | spec.md | user-story.md |
|---|---|---|---|
| AC-1 | Gate truth table: exactly one Graph POST when both flags on; zero writes and zero write-path token acquisitions on the other three rows | [x] | [x] |
| AC-2 | Flag-off no-behavior-change: no Graph request / dedupe row; `propose_new_time_disabled` audit with four time columns; send-path and F18 reschedule-path ActingFlags byte-identical | [x] | [x] |
| AC-3 | Eligibility fail-closed matrix: organizer-owned, proposals-disallowed, missing times, empty id, zero slots -> no intent, silent return, no audit/write | [x] | [x] |
| AC-4 | Successful write: POST tentativelyAccept (escaped principal+id), bearer, client-request-id = correlation id, body exactly sendResponse+proposedNewTime; one `proposed_new_time` audit; dedupe recorded; zero RecordMoveAsync / no series_moves | [x] | [x] |
| AC-5 | Fail-closed on adapter/Graph error: D5 matrix; `propose_new_time_failed` durable then rethrow, no dedupe; local adapter `NOT_SUPPORTED` with zero HTTP I/O and zero token acquisitions | [x] | [x] |
| AC-6 | Idempotency: second evaluation audits `dedupe_skipped`, no Graph request (key `{mailbox}:{messageId}:attendee-propose-new-time`) | [x] | [x] |
| AC-7 | Mocked-Graph contract suite exists (FakeHttpHandler, graph.example.test base): method/URL/headers, exact body + absent-property guardrail, 202-empty-body mapping, D5 samples, 429 exhaustion under FakeTimeProvider | [x] | [x] |
| AC-8 | Quality gates: line >= 85% / branch >= 75% maintained; >= 1 property test per new pure function; architecture tests pass unmodified; mutual exclusivity with F18 asserted both directions | [x] | [x] |
| AC-9 | Live-verification runbook exists; HI recorded in orchestrator state as `response: exception` with that `runbook_path` | [x] | [x] |

## Acceptance Criteria Evaluation

| # | Verdict | Evidence (verified in this review) |
|---|---|---|
| AC-1 | **PASS** | `SchedulingWorkerProposeNewTimeTests`: `GateTruthTable_DisabledRow_DryRunDisabledWithNoWrite` (3 DataRows: false/false, false/true, true/false) asserts `propose_new_time_disabled` and `Times.Never` on `ProposeNewMeetingTimeAsync`, `RecordMoveAsync`, and `RecordAsync`; `GateTruthTable_BothFlagsOn_WritesExactlyOnce` asserts `Times.Once` for `evt-1`. Zero write-path token acquisition on disabled rows follows from the service seam never being invoked. Gate source verified: `CalendarWritePolicy.AttendeeProposeNewTimeAllowed` = `CalendarWriteEnabled && EnableAttendeeProposeNewTime`, both default `false`. |
| AC-2 | **PASS** | `DryRun_Disabled_CarriesFourTimeColumnsAndProposeActingFlags` asserts result code, `EventId`, all four time columns, duration preservation, and the propose-specific ActingFlags snapshot `CalendarWriteEnabled=True;EnableAttendeeProposeNewTime=False`. Send/reschedule-path invariance verified by edge test `SendAndReschedulePaths_PersistUnmodifiedActingFlags_AfterF19` (both paths run in one evaluation and assert their pre-F19 flag strings), and by source: neither `BuildActingFlags` nor `BuildRescheduleActingFlags` is widened. No dedupe write on the dry-run asserted via `Times.Never`. |
| AC-3 | **PASS** | Edge test `NoIntent_ProducesNoProposeAuditAndNoServiceCall` with 6 DataRows (`null-event`, `organizer-owned`, `proposals-disallowed`, `missing-times`, `empty-id`, `zero-slots`) asserts no propose audit record and `Times.Never` on the service call. Corroborated by the property test `ComputeIntent_MissingPrecondition_NeverYieldsIntent` (1000 samples), which removes each precondition and asserts no intent. |
| AC-4 | **PASS** | Contract tests: POST method + escaped route `users/{Principal}/events/{escaped-id}/tentativelyAccept`; bearer auth; `client-request-id` equals the supplied request id; body top-level exactly `{sendResponse:true, proposedNewTime:{start,end}}` with UTC seconds-precision `dateTimeTimeZone` pairs and structural absence of `comment` and top-level `start`/`end`/`body`/`subject`/`attendees` (JsonDocument); 202-empty-body maps to `ok:true, data:null`. Worker side: `Success_AuditsProposedThenRecordsDedupe_InOrder_NoMove` asserts exactly one `proposed_new_time` record (action type `attendee-propose-new-time`, four time columns, non-empty correlation id), strict ordering audit -> dedupe record, and `Times.Never` on `RecordMoveAsync`. Correlation-id forwarding proven at the seam by `ProposeNewMeetingTimeAsync_ForwardsCorrelationIdAsRequestId`. |
| AC-5 | **PASS** | Contract suite covers D5 non-retryable samples (400/401/403/404 with Graph `error.code` passthrough, `Retryable == false`) and 429 retry exhaustion (3 attempts, Retry-After precedence, `FakeTimeProvider`). Worker failure path: `Failure_AuditsProposeNewTimeFailed_NoDedupe_NoMove` asserts `propose_new_time_failed` with `ErrorDetail`, and `Times.Never` on dedupe and `RecordMoveAsync`. Local adapter: `ProposeNewMeetingTime_LocalBackend_ReturnsNotSupportedWithoutHttpIo` asserts code `NOT_SUPPORTED`, `Retryable:false`, `handlerInvocations == 0` (zero HTTP I/O and zero token reads). |
| AC-6 | **PASS** | Edge test `DedupeHit_AuditsDedupeSkipped_NoWrite`: a pre-recorded dedupe key yields `dedupe_skipped` and `Times.Never` on the write. Key construction verified in source: `SentActionKey.Build(mailbox, messageId, SentActionKey.AttendeeProposeNewTime)` with `AttendeeProposeNewTime = "attendee-propose-new-time"`, recorded only after a successful write (step 5). |
| AC-7 | **PASS** | `GraphHostAdapterClientProposeNewTimeTests.cs` exists (352 lines) using the established `FakeHttpHandler` pattern with base address `https://graph.example.test/v1.0/`, covering method/route, headers (bearer, client-request-id, no Prefer), the exact two-property body with the absent-property guardrail, 202-empty-body mapping, D5 error samples, and 429 exhaustion under `FakeTimeProvider`. Coverage confirms the method fully exercised (100% line / 100% branch). All tests pass in this review's fresh run. |
| AC-8 | **PASS** | Independently re-measured (dual-mode): OpenClaw.Core 99.29% line / 92.28% branch (settings mode; baseline 99.27/92.24, no regression). Plain mode fully instruments the new async body: `SchedulingWorker.ProposeNewTime.cs` 100% line (145/145) / 93.75% branch; `GraphHostAdapterClient.ProposeNewTime.cs` 100%/100%; both new/modified async method bodies fully hit, all misses in modified files outside changed ranges. Both thresholds held repo-wide and per changed file. Property density: 3 CsCheck property tests covering both new pure functions (`ComputeProposeNewTimeIntent`, `BuildProposeNewTimeActingFlags`). Architecture: NetArchTest suites pass unmodified (930/930); zero `CloudGraph` references from the Agent partition (grep). Mutual exclusivity asserted both directions: `OrganizerOwnedMessage_TriggersOnlyReschedule_ZeroProposeCalls` and `AttendeeMessage_TriggersOnlyPropose_ZeroRescheduleCalls`. Full detail: policy audit sections 4, 5, 8. |
| AC-9 | **PASS** | The runbook exists at the exact path named in the AC (`runbooks/attendee-propose-new-time-live-verification.runbook.md`, verified on disk). `artifacts/orchestration/orchestrator-state.json` contains `human_interaction.requirements` with an entry whose `response` is `"exception"` and whose `runbook_path` equals that path; all three `orchestrator-state.md` invariants verified manually (requirements list present; response in enum; non-empty runbook_path). Supporting evidence record: `evidence/other/human-interaction-record.2026-07-07T05-56.md`. |

## Summary

- **AC verdicts: 9 PASS / 0 PARTIAL / 0 FAIL / 0 UNVERIFIED.**
- Toolchain: clean single pass reproduced in this review (CSharpier, analyzers/nullable under
  `TreatWarningsAsErrors`, architecture, 930 Core tests, dual-mode coverage).
- Blocking findings: **0**. One Minor and two Info non-blocking observations in
  `code-review.2026-07-07T06-35.md`.
- Remediation: not required; no `remediation-inputs` artifact produced.
- Go/no-go: **GO** — the branch is PR-ready against `epic/openclaw-vision-integration`. This
  completes Epic D and the openclaw-vision program's final feature (F19).

## Acceptance Criteria Check-off

All nine criteria were already checked `[x]` in both `spec.md` and `user-story.md` by the
executor. This review independently verified each criterion as PASS (table above), so every
existing check-off is evidence-backed and no source-file edits were made. No criteria remain
unchecked.

### Acceptance Criteria Status
- Source: `docs/features/active/2026-07-07-attendee-propose-new-time-130/spec.md` and `docs/features/active/2026-07-07-attendee-propose-new-time-130/user-story.md`
- Total AC items: 9 (mirrored identically in both files; 18 checkboxes total)
- Checked off (delivered): 9
- Remaining (unchecked): 0
- Items remaining: none
