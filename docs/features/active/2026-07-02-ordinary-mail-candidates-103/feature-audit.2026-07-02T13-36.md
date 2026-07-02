# Feature Audit: ordinary-mail-candidates (#103)

**Audit Date:** 2026-07-02
**Branch:** `feature/ordinary-mail-candidates-103` @ `0f346d5e74a5543526ba8e642fe684b73475dba3`
**Work Mode:** `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`)
**AC Sources:** `spec.md` and `user-story.md` (identical AC sets; mirrored in `issue.md`)

## Scope and Baseline

- Resolved base branch: `main` (origin/main; the local `main` ref is stale per caller inputs).
- Merge-base SHA: `3dae644d98dcb564767002a51503e6b9944e4eab`; branch head: `0f346d5e74a5543526ba8e642fe684b73475dba3` (single commit).
- Evidence sources: `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` (refreshed for this head), the authoritative `git diff 3dae644..0f346d5` (29 files, +1934/-5; 4 production `.cs`, 6 test `.cs`), executor evidence under `docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/`, and the reviewer's independent toolchain re-run at branch head (format EXIT 0; build 0/0; 731 tests passed / 0 failed / 5 env-gated skips; fresh cobertura parsed per changed file — `evidence/qa-gates/coverage-review.2026-07-02T13-36.md`).
- Note: the PR-context summary's file categorization ("Core logic changes: 0 files") is inaccurate for this branch; the audit scoped from the authoritative git diff (recorded in the policy audit's Rejected Scope Narrowing section).

## Acceptance Criteria Inventory

Seven checkbox criteria, identical in `spec.md` (lines 123-129) and `user-story.md` (lines 45-51), mirrored in `issue.md` (lines 25-31). All arrived checked `[x]` by the executor.

1. AC-1 — Candidate source returns ordinary mail alongside meeting messages (kind `"all"`), preserving lookback/limit/ordering; new unit test against in-memory SQLite `CoreCacheRepository`.
2. AC-2 — New pure static `RelatedEventMatcher` ports Section 9.2 exactly (tokens >= 4 chars at +2, participant emails at +3, threshold 4, tie-break earliest `Start` null-last then ordinal `Id`); unit tests cover the named rows.
3. AC-3 — CsCheck property tests: null-or-score>=4, permutation invariance, case-insensitivity, duplicate set semantics.
4. AC-4 — On direct-lookup miss, `SchedulingWorker.ProcessMessageAsync` fetches `GetCalendarViewAsync(now, now + CalendarViewFallbackDays)`; new `AgentPolicyOptions` value default 14; `now` from injected `TimeProvider`; non-positive skips; verified with mocked service and `FakeTimeProvider`.
5. AC-5 — No-match falls through to message-only `Normalize(mailboxUpn, message, null)` without a new exception path; a match hydrates through the same `Normalize` call.
6. AC-6 — Widened candidates re-listed every cycle; sends deduplicated by the #101 store with unchanged key shape; dedupe tests extended with an ordinary-mail scenario.
7. AC-7 — Full C# toolchain passes; coverage thresholds hold with changed lines covered; all touched files <= 500 lines.

## Acceptance Criteria Evaluation

| # | Criterion | Verdict | Evidence |
|---|-----------|---------|----------|
| AC-1 | Candidate widening to `item_kind = 'mail'` with unchanged lookback/limit/ordering | PASS | Diff: kind literal `"meeting"` -> `"all"` plus doc update, nothing else changed in the method. Repository predicate for `"all"` pre-existed. 4 new tests against a real in-memory shared-cache SQLite `CoreCacheRepository` (inclusion, lookback, limit, ordering). Fail-before evidence: `expect-fail-candidate-widening.2026-07-02T13-11.md` EXIT 1 with exactly the 2 widening-discriminating tests failing and the 2 behavior-preserving tests passing. Reviewer re-run green. |
| AC-2 | Pure `RelatedEventMatcher` exact Section 9.2 port with deterministic tie-break | PASS | `src/OpenClaw.Core/Agent/RelatedEventMatcher.cs` (new, 191 lines): lowercase split on `[^a-z0-9]+`, length >= 4, distinct; participants From/Sender/To/Cc via `MeetingContextNormalizer.EmailOf`; attendee union required+optional+resource, organizer excluded; +2/+3 weights; threshold 4; tie-break earliest `Start` (null last) then ordinal `Id` (null as empty) in `PrecedesInTieBreak`. 14 unit tests cover every named row (subject-only, attendee-only, combined, sub-threshold, exact-threshold, empty-input, both tie-break axes). Reviewer-parsed coverage 100.00% line / 89.58% branch. |
| AC-3 | CsCheck property/invariant tests for the matcher | PASS | `RelatedEventMatcherPropertyTests.cs` (new): 4 properties at 1000 iterations — result null-or-score>=4; permutation invariance (seeded Fisher-Yates shuffle); case-insensitivity (uppercasing subjects/emails); duplicate tokens/emails do not change the score. Genuine CsCheck `Gen`/`Sample` usage matching the suite convention; satisfies the T1 property-test density gate for the new pure function. |
| AC-4 | Calendar-view fallback with `CalendarViewFallbackDays` (default 14), TimeProvider-derived `now`, non-positive opt-out | PASS | Diff: guarded block in `ProcessMessageAsync` (`meetingEvent is null && options.CalendarViewFallbackDays > 0`) and `ChooseRelatedEventFromWindowAsync` using `timeProvider.GetUtcNow()` for both bounds. `AgentPolicyOptions.CalendarViewFallbackDays` default 14 with Section 9.2 XML doc. Tests: exact window-bounds verification against `FakeTimeProvider` `Now`..`Now.AddDays(14)`; opt-out DataRows (0, -1) verify the fetch never happens. Fail-before evidence: `expect-fail-worker-fallback.2026-07-02T13-11.md` EXIT 1 (3 discriminating tests). |
| AC-5 | No-match falls through to message-only triage via the same `Normalize` call; match hydrates identically | PASS | Diff shows a single `MeetingContextNormalizer.Normalize(MailboxUpn(), message, meetingEvent)` call downstream of the fallback — no duplicate normalize path, no new catch blocks. Tests: empty window, all-below-threshold, and failure-envelope-mapped-to-empty-window each proceed without exception and produce the message-derived reply subject; the match test proves event hydration through the event-derived reply subject (`Re: Project sync planning`). |
| AC-6 | Cross-cycle re-listing with unchanged #101 dedupe key shape, ordinary-mail dedupe scenario | PASS | `RunCycle_OrdinaryMailAcrossTwoCycles_SendsOnceWithUnchangedKeyShape` (new): plain-mail candidate (null `MeetingMessageType`) re-listed across two cycles with a stateful `ISentActionStore` double; asserts `SendMailAsync` exactly once, `IsRecordedAsync(mailKey)` exactly twice, `RecordAsync(mailKey, Now)` exactly once, and the recorded key set equals exactly `[SentActionKey.Build("owner@contoso.com", "mail-1", SentActionKey.ProposalReply)]` — the unchanged #101 key shape. No production change to `SentActionKey` (absent from diff). |
| AC-7 | Full toolchain + coverage thresholds + 500-line caps | PASS | Reviewer re-ran at head `0f346d5`: `csharpier check .` EXIT 0 (217 files); `dotnet build` 0 warnings / 0 errors (analyzers + nullable as errors); NetArchTest boundary tests inside the 284/284 Core.Tests pass; full suite 731 passed / 0 failed / 5 env-gated skips; pooled coverage 90.81% line / 80.62% branch (gates 85/75); no regression (+0.18pp/+0.37pp; per-file figures equal or exceed baseline); all 10 touched `.cs` files <= 500 lines (max 364, `wc -l` reviewer-verified). Async-fallback instrumentation exclusion documented and behaviorally covered (policy audit Section 8). |

## Summary

All seven acceptance criteria evaluate PASS with independently re-verified evidence: the diff delivers exactly the spec's implementation scope (four production files), both delivered behaviors carry discriminating fail-before/pass-after regression evidence, the new pure function meets the T1 property-test obligation with four genuine CsCheck properties, and the reviewer's fresh toolchain and coverage runs reproduce the executor's numbers exactly (pooled 90.81% line / 80.62% branch; new matcher file 100.00% line / 89.58% branch). No Blocking or Major findings; the code review records one Minor follow-up (TimeProvider injection for the pre-existing wall-clock anchor in `CacheSchedulingCandidateSource`) and two Info observations. Remediation is not required. Recommendation: **Go for PR**.

## Acceptance Criteria Check-off

All seven criteria were already checked `[x]` by the executor in both authoritative source files (`spec.md`, `user-story.md`) and the `issue.md` mirror. Per the acceptance-criteria-tracking protocol, the reviewer verified each PASS verdict against the checked state; no new check-offs were needed and no criterion required unchecking. Criterion text was not modified.

### Acceptance Criteria Status
- Source: `docs/features/active/2026-07-02-ordinary-mail-candidates-103/spec.md`, `docs/features/active/2026-07-02-ordinary-mail-candidates-103/user-story.md` (mirror: `issue.md`)
- Total AC items: 7
- Checked off (delivered): 7
- Remaining (unchecked): 0
- Items remaining: none
