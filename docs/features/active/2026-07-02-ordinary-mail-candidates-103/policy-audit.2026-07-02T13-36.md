# Policy Compliance Audit: ordinary-mail-candidates (#103)

**Audit Date:** 2026-07-02
**Code Under Test:** C# only. 4 production `.cs` files in `src/OpenClaw.Core` (`Agent/RelatedEventMatcher.cs` NEW, 191 lines, pure static matcher; `Agent/Contracts/AgentPolicyOptions.cs` adding `CalendarViewFallbackDays` default 14; `Agent/Runtime/CacheSchedulingCandidateSource.cs` kind literal `"meeting"` -> `"all"` plus doc update; `Agent/Runtime/SchedulingWorker.Pipeline.cs` adding the calendar-view fallback block and `ChooseRelatedEventFromWindowAsync`), 6 test `.cs` files (4 new: `RelatedEventMatcherTests.cs`, `RelatedEventMatcherPropertyTests.cs`, `Runtime/CacheSchedulingCandidateSourceTests.cs`, `Runtime/SchedulingWorkerFallbackTests.cs`; 2 modified: `Runtime/SchedulingWorkerTests.cs`, `Runtime/SchedulingWorkerDedupeTests.cs`). Plus feature scoping/evidence Markdown (feature folder for issue #103) and orchestrator agent-memory Markdown. No Python, PowerShell, TypeScript, Bash, or governed JSON files changed in the branch diff.

**Scope:** Full feature branch `feature/ordinary-mail-candidates-103` @ `0f346d5e74a5543526ba8e642fe684b73475dba3` versus resolved base `main` @ merge-base `3dae644d98dcb564767002a51503e6b9944e4eab` (origin/main; the local `main` ref is stale per the caller inputs). Scope is feature-vs-base over the complete branch diff. Diff file breakdown (name-only): 10 `.cs`, 19 `.md` (29 files, +1934/-5). Work mode: `full-feature` (persisted marker `- Work Mode: full-feature` in `issue.md`); acceptance-criteria sources are `spec.md` and `user-story.md` (mirrored in `issue.md`).

**Coverage Metrics by Language:**

| Language | Files Changed | Tests | Test Result | Baseline Coverage | Post-Change Coverage | New Code Coverage |
|----------|--------------|-------|-------------|-------------------|---------------------|-------------------|
| C# | 4 production `.cs` + 6 test `.cs` | 736 (solution) / 284 (Core.Tests) | 731 pass, 0 fail, 5 env-gated skips | 90.63% line, 80.25% branch (pooled solution) | 90.81% line, 80.62% branch (pooled solution) | RelatedEventMatcher.cs (new) 100.00% line / 89.58% branch; CacheSchedulingCandidateSource.cs 100.00% line, no branch points, identical to baseline; SchedulingWorker.Pipeline.cs 100% of instrumented lines with the two partial branches pre-existing and untouched (async fallback body uninstrumented per pre-existing runsettings attribute exclusion, behaviorally covered by 7 fallback tests with fail-before evidence); AgentPolicyOptions.cs auto-properties uninstrumented at baseline and post-change alike, behaviorally covered |

**Note:** Python, PowerShell, Bash, TypeScript, and governed-JSON rows are omitted because the branch diff contains no changed files in those languages. Coverage verdicts are therefore C#-only; no other language has changed files on the branch. The C# coverage verdict is an explicit PASS.

### Coverage Evidence Checklist

- C# baseline coverage artifact: `docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/baseline/baseline-dotnet-test.2026-07-02T13-02.md` (pooled 90.63% line / 80.25% branch; raw cobertura at `artifacts/csharp/baseline-2026-07-02T13-02/`)
- C# post-change coverage artifact: `docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/qa-gates/final-qa-dotnet-test.2026-07-02T13-21.md` and `evidence/qa-gates/coverage-comparison.2026-07-02T13-21.md` (pooled 90.81% line / 80.62% branch)
- Reviewer-regenerated cobertura (this audit, fresh `dotnet test` at branch head): `docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/qa-gates/coverage-review/{2300629f...,4f1a1449...,75d17a89...}/coverage.cobertura.xml`; independently parsed pooled 90.81% line (4298/4733) / 80.62% branch (990/1228), identical to executor evidence. Reviewer evidence: `docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/qa-gates/coverage-review.2026-07-02T13-36.md`
- Per-language comparison summary: Section 1.2.1 below
- TypeScript baseline coverage artifact: `N/A - out of scope`
- TypeScript post-change coverage artifact: `N/A - out of scope`
- PowerShell baseline coverage artifact: `N/A - out of scope`
- PowerShell post-change coverage artifact: `N/A - out of scope`
- Python / TypeScript / PowerShell coverage artifacts: `N/A - no changed files in those languages on the branch`

**Non-negotiable verdict rule:** This audit includes numeric baseline and post-change coverage metrics for the only in-scope language (C#), plus per-changed-file line AND branch coverage re-measured by the reviewer from fresh cobertura. The C# coverage gate is met (pooled line 90.81% >= 85%, branch 80.62% >= 75%; the new matcher file is at 100.00% line / 89.58% branch; every instrumented changed line is covered; no regression — pooled coverage improved by +0.18pp line / +0.37pp branch and every changed file's instrumented figures equal or exceed baseline).

---

## Executive Summary

This feature branch closes issue #103 (gap F7): ordinary scheduling mail is now triaged. Three coordinated changes: (a) `CacheSchedulingCandidateSource.GetCandidateMessageIdsAsync` widens the candidate predicate from `"meeting"` to `"all"` (meeting plus ordinary mail; lookback, limit, and recency ordering unchanged); (b) a new pure static `RelatedEventMatcher` in `OpenClaw.Core.Agent` ports master Section 9.2 `chooseMostLikelyRelatedEvent` (subject tokens length >= 4 at +2, participant emails at +3, accept threshold 4, deterministic tie-break by earliest `Start` null-last then ordinal `Id` null-as-empty); (c) `SchedulingWorker.ProcessMessageAsync` gains a calendar-view fallback — on a direct event-lookup miss with `CalendarViewFallbackDays > 0` it fetches `GetCalendarViewAsync(now, now + CalendarViewFallbackDays)` from the injected `TimeProvider` and applies the matcher, with no-match falling through to the existing message-only `Normalize(mailboxUpn, message, null)` call. No new exception paths; all I/O remains behind the `ISchedulingService` seam; the #101 sent-action dedupe store and `SendEnabled`/`CalendarWriteEnabled` kill switches bound side effects.

The mandatory toolchain was independently re-run by the reviewer against the branch head `0f346d5` and passes in a single pass:
- **Formatting:** `csharpier check .` (CSharpier 1.3.0) — "Checked 217 files", EXIT 0, no diffs.
- **Lint + nullable type-check + analyzers:** `dotnet build OpenClaw.MailBridge.sln` — Build succeeded, 0 Warning(s), 0 Error(s) (AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true per Directory.Build.props).
- **Architecture-boundary tests:** NetArchTest suite runs inside `OpenClaw.Core.Tests` — included in the 284/284 pass.
- **Tests + coverage:** full solution `dotnet test` with `--collect:"XPlat Code Coverage"` — 731 passed, 0 failed, 5 environment-gated skips (same skips as baseline); pooled coverage 90.81% line / 80.62% branch, above the uniform gates; T1 property-test obligation for the new pure function satisfied by `RelatedEventMatcherPropertyTests` (CsCheck, 4 properties, 1000 iterations each, seeded Fisher-Yates permutation).
- **Regression evidence:** fail-before artifacts (EXIT 1) for both the candidate widening (2 discriminating tests failing pre-change) and the worker fallback (3 discriminating tests failing pre-change) are present under `evidence/regression-testing/`, with the final suite green.

No Blocking findings. No material PARTIAL findings. Two informational/minor observations (async-body instrumentation scope — pre-existing runsettings behavior, same disposition as the accepted #99 audit; and the pre-existing `DateTimeOffset.UtcNow` anchor in `CacheSchedulingCandidateSource` that forces wall-clock-relative seeding in its new tests; Section 8 and the code review). Remediation is not required. The feature is recommended Go for PR.

**Policy documents evaluated:**
- `.claude/rules/general-code-change.md`
- `.claude/rules/general-unit-test.md`
- `.claude/rules/quality-tiers.md`
- `.claude/rules/architecture-boundaries.md`
- `.claude/rules/csharp.md`
- `.claude/rules/ci-workflows.md` (not triggered — no workflow changes)
- `.claude/rules/benchmark-baselines.md` (not triggered — no baseline changes)
- `.claude/rules/tonality.md`

**Language-specific policies evaluated:**
- C#: `.claude/rules/csharp.md`
- N/A Python / PowerShell / Bash / TypeScript / governed JSON (no changed files on the branch)

**Temporary artifacts cleanup:**
- No temporary or throwaway scripts were introduced by this feature; the diff is four production files, six test files, and documentation/evidence Markdown. The executor's raw cobertura intermediates under `artifacts/csharp/` are untracked (gitignored) and do not appear in the diff.

---

## Rejected Scope Narrowing

None. The caller prompt instructed execution of the full `feature-review-workflow` contract, supplied the authoritative base branch (`main`), merge-base SHA (`3dae644`), the checked-out feature branch, and refreshed PR-context artifacts, and stated "Scope determination is your responsibility per your skill contract." No instruction attempted to narrow scope to a plan/task/phase subset, to a file subset, or to mark any in-scope language as out-of-scope or informational-only.

Observation (not a narrowing instruction, recorded for completeness): the PR-context summary's "Changed files overview" reports "Core logic changes: 0 files" and categorizes the branch as docs/tooling only (16 files). That categorization is inaccurate; the authoritative `git diff 3dae644..0f346d5` contains 4 production C# files and 6 test C# files (29 files total, +1934/-5). This is the third consecutive review (#99, #101, #103) where the summary miscategorizes a C# branch as docs-only. The audit used the authoritative git diff file list, not the summary categorization. No scope was narrowed.

---

## Evidence Location Compliance

The branch diff was scanned for evidence files written under the non-canonical roots `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/evidence/`, `artifacts/coverage/`, `artifacts/regression-testing/`, or `artifacts/post-change/`.

- Command: `git diff --name-only 3dae644..HEAD | grep -E '^artifacts/'`
- Result: **NONE.** No files under `artifacts/` are tracked in the diff at all. All feature evidence in the diff is written to the canonical `docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/<kind>/` locations (baseline, qa-gates, regression-testing).
- Verdict: **PASS** — no evidence-location violations. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events occurred during this review; the reviewer's own evidence was written to the canonical `evidence/qa-gates/` path.

Note: the repository does not contain a `validate_evidence_locations.py` script (consistent with the prior #70, #80, #19, #18, #99, and #101 audits); the scan was performed by direct diff inspection. The executor's untracked raw cobertura copies under `artifacts/csharp/baseline-2026-07-02T13-02/` and `artifacts/csharp/final-2026-07-02T13-21/` are non-evidence coverage tooling intermediates at a path the feature-review skill itself designates for C# coverage; the canonical feature evidence lives under `evidence/baseline/` and `evidence/qa-gates/`.

---

## 1. General Unit Test Policy Compliance

### 1.1 Core Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Independence** — Tests run in any order | PASS | Each matcher test builds its own DTOs; each candidate-source test creates a uniquely named in-memory shared-cache SQLite database (`candidate-source-{Guid}`); each fallback/dedupe/worker test builds a fresh Moq graph. 284/284 Core.Tests pass in a single reviewer run. |
| **Isolation** — Each test targets single behavior | PASS | 14 matcher unit tests each pin one scoring/tie-break/guard rule; 4 candidate-source tests pin inclusion, lookback, limit, ordering separately; 7 fallback test cases pin window bounds, hydration, empty window, opt-out (x2), sub-threshold, failure degradation individually. |
| **Fast Execution** | PASS | `OpenClaw.Core.Tests` completes 284 tests in ~1 s (reviewer run); property tests are in-memory CsCheck sampling; SQLite tests use in-memory shared cache. |
| **Determinism** | PASS | Fallback tests use `FakeTimeProvider` pinned to a fixed `Now`; property tests use CsCheck's seeded sampling with the permutation shuffle driven by a generated seed through a local `Random(seed)` (reproducible); no sleeps, timers, network, or filesystem. The candidate-source tests anchor seed rows to `DateTimeOffset.UtcNow` because the production class reads the wall clock directly (pre-existing behavior, line unchanged by this branch); margins are generous (1-4h offsets against a 48h window, 100h for the stale row), so the tests are deterministic in practice. Recorded as a Minor follow-up in the code review. |
| **Readability & Maintainability** | PASS | Descriptive snake-case scenario names (`Subject_only_match_with_two_shared_tokens_scores_four_and_is_accepted`, `RunCycle_NonPositiveFallbackDays_NeverFetchesCalendarView`), FluentAssertions with because-messages, XML doc summaries on every new test class citing the AC each covers. |

### 1.2 Coverage and Scenarios

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Baseline Coverage Documented** | PASS | Baseline pooled: 90.63% line (4207/4642), 80.25% branch (947/1180). Source: `evidence/baseline/baseline-dotnet-test.2026-07-02T13-02.md`; independently re-parsed by the reviewer from `artifacts/csharp/baseline-2026-07-02T13-02/`. |
| **No Coverage Regression** | PASS | Post-change pooled: 90.81% line, 80.62% branch (+0.18pp / +0.37pp). Per changed file: candidate source 100% -> 100% line; pipeline 20/20 line and 2/4 branch at baseline and post-change with the two partial conditions verified to be the same pre-existing `MailboxUpn`/`BuildProposalReply` ternaries (baseline lines 170/177 = post-change lines 233/240 after the +63-line shift). Independently confirmed from reviewer cobertura. |
| **New Code Coverage** | PASS | `RelatedEventMatcher.cs` (new): 100.00% line (91/91) / 89.58% branch (43/48) — reviewer-parsed, line AND branch. The 5 uncovered condition arms (lines 180, 186, 189) are nullable-lifted compiler branches in the tie-break comparisons (`Start` lifted operators, `Id ?? string.Empty` null arms); above both gates. The new async fallback in `SchedulingWorker.Pipeline.cs` is uninstrumented per the pre-existing runsettings CompilerGenerated exclusion and is behaviorally verified by 7 fallback tests plus fail-before evidence (see Section 8). |
| **Comprehensive Coverage** | PASS | Candidate widening (inclusion/lookback/limit/ordering), matcher scoring rows and tie-breaks, four CsCheck invariants, fallback window bounds/hydration/no-match/opt-out/failure degradation, and cross-cycle ordinary-mail dedupe are all exercised. |
| **Positive Flows** | PASS | Mail-alongside-meeting inclusion; subject-only/attendee-only/combined/exact-threshold acceptance rows; window match hydrating the event context observable in the outbound reply subject. |
| **Negative Flows** | PASS | Sub-threshold score returns null; `ArgumentNullException` for null message and null events; empty window and all-below-threshold fall through to message-only triage; stale rows excluded by lookback. |
| **Edge Cases** | PASS | Exact threshold (score 4) accepted; score 3 rejected; null/short-token subjects; case-insensitive tokens and emails; organizer excluded from attendee scoring; tie-break earliest-Start with null-Start-last; tie-break ordinal-Id when Start ties; empty event list; limit cap; recency interleaving across kinds; non-positive fallback days (0 and -1). |
| **Error Handling** | PASS | Failure-envelope-mapped-to-empty-window degradation proceeds without exception and the pipeline continues (mailbox settings consulted); null-arg guards pinned; no new exception paths introduced (verified in diff — no catch blocks added). |
| **Concurrency** | N/A | No new concurrency surface; the fallback is a single awaited seam call inside the existing per-message pipeline. |
| **State Transitions** | PASS | Cross-cycle dedupe state proven with a stateful `ISentActionStore` double: consult-before-send twice, record-once, key set exactly `[mailKey]` with the unchanged #101 key shape. |

### 1.2.1 Per-Language Coverage Comparison

- C#: Baseline: 90.63% line, 80.25% branch (pooled solution) -> Post-change: 90.81% line, 80.62% branch. Change: +0.18% line, +0.37% branch. New/changed-code coverage: RelatedEventMatcher.cs (new) 100.00% line / 89.58% branch reviewer-parsed; CacheSchedulingCandidateSource.cs 100.00% line identical to baseline; SchedulingWorker.Pipeline.cs 100% of instrumented lines with both partial branches pre-existing and untouched, new async fallback behaviorally covered by 7 tests with fail-before evidence; AgentPolicyOptions.cs auto-properties uninstrumented at baseline and post-change alike, defaults behaviorally covered; new test files excluded from measurement per policy. Disposition: PASS (line >= 85%, branch >= 75%, no regression on changed lines). Evidence: `evidence/baseline/baseline-dotnet-test.2026-07-02T13-02.md`, `evidence/qa-gates/final-qa-dotnet-test.2026-07-02T13-21.md`, `evidence/qa-gates/coverage-comparison.2026-07-02T13-21.md`, reviewer re-run `evidence/qa-gates/coverage-review.2026-07-02T13-36.md`.

### 1.3 Test Structure and Diagnostics

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clear Failure Messages** | PASS | FluentAssertions because-clauses on every material assertion ("the widened candidate set is meeting plus ordinary mail within the window", "no other key shape may be recorded"); CsCheck prints the failing seed per suite convention; Moq `Verify` failures name the exact seam call and cardinality. |
| **Arrange-Act-Assert Pattern** | PASS | All new tests carry explicit `// Arrange` / `// Act` / `// Assert` comments with AC anchors. |
| **Document Intent** | PASS | XML docs on all four new test classes state the AC(s) covered, the no-temp-files SQLite strategy, and the wall-clock-anchor rationale where applicable. |

### 1.4 External Dependencies and Environment

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Avoid External Dependencies** | PASS | No network, COM, or external process; repository tests use in-memory shared-cache SQLite (`Mode=Memory;Cache=Shared`, per the established `CoreCacheRepository*Tests` convention); all seams mocked with Moq. |
| **Use Mocks/Stubs** | PASS | `ISchedulingService`, `ISchedulingCandidateSource`, and `ISentActionStore` mocked; window stubs are explicit (`ReturnsAsync(Array.Empty<SchedulingEventDto>())`) rather than Moq loose defaults, per the spec's stated risk mitigation. |
| **Environment Stability** | PASS | No temporary files (in-memory SQLite only); no mutable global state; no environment variables read. |

### 1.5 Policy Audit Requirement

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Pre-submission Review** | PASS | This audit serves as the required policy review. No outstanding items. |

---

## 2. General Code Change Policy Compliance

### 2.1 Before Making Changes

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Clarify the objective** | PASS | Issue #103, `spec.md` v0.2 (six design decisions D1-D6 with verified code anchors), user-story scenarios, and the F7 gap-analysis reference define the change precisely. |
| **Read existing change plans** | PASS | `evidence/baseline/phase0-instructions-read.md` records the policy-order read; `plan.2026-07-02T12-43.md` present. |
| **Document the plan** | PASS | `plan.2026-07-02T12-43.md` with per-phase evidence under `evidence/**`; completed tasks recorded in the PR-context summary. |

### 2.2 Design Principles

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Simplicity first** | PASS | The widening is a one-literal change; the matcher is one pure static class with fixed weights; the fallback is one guarded block plus one private method; no new types beyond the matcher, no new interfaces, no new dependencies. |
| **Reusability** | PASS | Email normalization reuses the existing public `MeetingContextNormalizer.EmailOf`; tokenization follows the established `GeneratedRegex` pattern; the internal `ChooseMostLikelyRelatedEventWithScore` overload lets the pipeline log the score without recomputing. |
| **Extensibility** | PASS | `CalendarViewFallbackDays` is an options-bag value with a documented default and opt-out; the matcher's public surface is the single spec-sanctioned method; the fallback sits behind the existing `ISchedulingService` seam so the Graph swap carries it unchanged. |
| **Separation of concerns** | PASS | Scoring/selection is pure in `OpenClaw.Core.Agent`; the window fetch (I/O) is confined to the Runtime pipeline through the seam; per D3 the service adapter stays thin. |

### 2.3 Module & File Structure

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Cohesive modules** | PASS | Pure logic in `Agent/`, orchestration in `Agent/Runtime/`, options in `Agent/Contracts/`; tests mirror the production layout exactly. |
| **Under 500 lines** | PASS | `wc -l` (reviewer): RelatedEventMatcher.cs 191, AgentPolicyOptions.cs 95, CacheSchedulingCandidateSource.cs 29, SchedulingWorker.Pipeline.cs 258, RelatedEventMatcherTests.cs 257, RelatedEventMatcherPropertyTests.cs 273, CacheSchedulingCandidateSourceTests.cs 175, SchedulingWorkerFallbackTests.cs 312, SchedulingWorkerTests.cs 330, SchedulingWorkerDedupeTests.cs 364. All under the 500-line cap (max 364); matches executor evidence `evidence/qa-gates/final-qa-scope-and-caps.2026-07-02T13-21.md`. |
| **Public vs internal** | PASS | The only public-surface additions are `RelatedEventMatcher.ChooseMostLikelyRelatedEvent` (spec-sanctioned) and the `CalendarViewFallbackDays` option; the with-score overload is `internal`; `ChooseRelatedEventFromWindowAsync` is `private`. |
| **No circular dependencies** | PASS | No project-reference changes; NetArchTest boundary suite passes inside the 284/284 Core.Tests run. |

### 2.4 Naming, Docs, and Comments

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Descriptive names** | PASS | `RelatedEventMatcher`, `ChooseMostLikelyRelatedEvent` (mirrors the master reference name), `CalendarViewFallbackDays`, `PrecedesInTieBreak`; named constants (`SubjectTokenWeight`, `ParticipantEmailWeight`, `AcceptThreshold`, `MinimumTokenLength`) instead of magic numbers. |
| **Docs/docstrings** | PASS | XML docs on the matcher class, both entry points, and every private helper; the options doc cites master Section 9.2 and documents the opt-out; the candidate-source summary updated to describe the widened set (spec requirement). |
| **Comment why, not what** | PASS | Comments explain the lowercase-before-regex contract, the organizer-exclusion rationale ("matching the master reference which scores event.attendees"), and the deliberate any-miss (not mail-only) fallback guard citing master Section 9.2/9.3. |

### 2.5 After Making Changes — Toolchain Execution

| Requirement | Status | Evidence |
|------------|--------|----------|
| **1. Formatting** | PASS | Reviewer: `csharpier check .` — Checked 217 files, EXIT 0. Executor: `evidence/qa-gates/final-qa-csharpier-check.2026-07-02T13-21.md` EXIT 0. |
| **2. Linting** | PASS | Reviewer: `dotnet build OpenClaw.MailBridge.sln` — 0 warnings, 0 errors (analyzers as errors). |
| **3. Type checking** | PASS | Same build; nullable reference analysis runs as errors per Directory.Build.props; clean. |
| **4. Architecture** | PASS | NetArchTest boundary tests pass within the full Core.Tests run (284/284). No COM, VSTO, or interop references added; all I/O remains behind the `ISchedulingService` seam (verified in diff — the pipeline gained no new dependency). |
| **5. Testing** | PASS | Reviewer: full solution test run — 731 passed, 0 failed, 5 environment-gated skips (identical to baseline skips). Includes the T1 property-based tests for the new pure function. |
| **6. Contract/schema checks** | N/A | No governed contract, DTO, route, or schema surface changed; `ISchedulingService` already carried `GetCalendarViewAsync` (interface byte-identical to base). |
| **7. Integration tests** | N/A | No adapter/external-system boundary changed; the repository-level behavior is covered against a real in-memory SQLite `CoreCacheRepository`, and the pipeline behavior at the mocked `ISchedulingService` seam per spec. |
| **Full toolchain loop** | PASS | Reviewer re-ran format -> build -> arch -> test+coverage in a single clean pass with no file mutations; executor evidence records the same single-pass Phase 5 loop (qa-gates set at 2026-07-02T13-21). |
| **Explicit reporting** | PASS | Commands and results documented here, in Appendix B, and in `evidence/qa-gates/coverage-review.2026-07-02T13-36.md`. |

### 2.6 Summarize and Document

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Summarize changes** | PASS | `spec.md` Implementation Strategy matches the delivered diff exactly (four production files, four new test files, two extended test files); the commit message describes the feature. |
| **Design choices explained** | PASS | D1 (tie-break deviation from the reference, recorded deliberately with rationale), D2 (client path over Core-cache read, five evidence points), D3 (fallback placement), D4 (no cursor exists — verified), D5 (no memoization, simplicity-first), D6 (Prefer-header non-goal) all documented in spec.md. |
| **Update supporting documents** | PASS | Acceptance criteria checked off in `spec.md`, `user-story.md`, and the `issue.md` mirror. |
| **Provide next steps** | PASS | Spec Constraints & Risks records the accepted Local-MVP volume risk and the possible per-kind quota follow-up. |

---

## 3. Language-Specific Code Change Policy Compliance

Only Section 3 C# applies. Python, PowerShell, Bash, TypeScript, and governed-JSON sections are omitted: no changed files in those categories on the branch.

### Section 3-C#: C# Code Change Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Formatting — CSharpier** | PASS | `csharpier check .` EXIT 0 (reviewer, CSharpier 1.3.0 global; the repo tool-manifest restore mismatch is a pre-existing environment accommodation also recorded in the #70, #80, #19, #18, #99, and #101 audits). |
| **Linting — .NET analyzers** | PASS | `dotnet build` clean: 0 warnings / 0 errors with AnalysisLevel=latest-all, AnalysisMode=All, TreatWarningsAsErrors=true. |
| **Type Checking — Nullable** | PASS | Nullable enabled solution-wide; new code uses `ArgumentNullException.ThrowIfNull`, `is null`/`is not null` patterns, and explicit null-handling in the tie-break (`Id ?? string.Empty`, null-Start ordering). |
| **Null-safety** | PASS | Matcher never throws for degenerate content (null subjects, empty attendee lists, null ids — pinned by tests); null-arg guards on both public inputs; options value is a non-nullable int with a documented non-positive opt-out. |
| **Async / resource safety** | PASS | `await ... .ConfigureAwait(false)` throughout the new pipeline code; cancellation token forwarded to the seam; no fire-and-forget, no blocking waits, no `async void`. |
| **Naming / file-scoped namespaces** | PASS | File-scoped namespaces; `Async` suffix on the new private method; PascalCase publics; `GeneratedRegex` partial-class pattern matches the existing `MeetingContextNormalizer.Helpers.cs` convention. |
| **Exceptions fail-fast** | PASS | `ArgumentNullException.ThrowIfNull` at the pure entry point; no catch blocks added anywhere in production; failure degradation reuses the existing envelope-to-empty-list mapping and the existing per-message isolation guard. |
| **No new suppressions** | PASS | The diff contains no pragma or suppression attributes and no banned-API additions (grep of the full C# diff for pragma, SuppressMessage, DateTime.Now, DateTime.UtcNow, Random.Shared, Thread.Sleep, Task.Delay returned zero added lines). The wall-clock read pre-existing in `CacheSchedulingCandidateSource` is unchanged (context line, not an addition) — see Section 8. |

Note on test framework: `.claude/rules/csharp.md` names xUnit/NSubstitute, but the repository's actual convention is MSTest + FluentAssertions + Moq (all existing suites; divergence recorded in `.claude/agent-memory/prd-feature/project_test_framework_discrepancy.md`). The new and modified tests follow the established repo convention, consistent with the prior validated #70, #80, #19, #18, #99, and #101 audits. Pre-existing repo-wide divergence, not a finding against this branch.

---

## 4. Language-Specific Unit Test Policy Compliance

Only C# tests changed. Python, PowerShell, and TypeScript sections are omitted.

### Section 4-C#: C# Unit Test Policy Compliance

| Requirement | Status | Evidence |
|------------|--------|----------|
| **Framework (repo convention: MSTest + FluentAssertions + Moq)** | PASS | `[TestClass]`/`[TestMethod]`/`[DataRow]`; FluentAssertions matchers; Moq `Setup`/`Verify`/`Capture.In` per suite convention. |
| **Test file location** | PASS | All test changes under `tests/OpenClaw.Core.Tests/Agent/` and `tests/OpenClaw.Core.Tests/Agent/Runtime/`, mirroring `src/OpenClaw.Core/Agent/` and `src/OpenClaw.Core/Agent/Runtime/`. No colocation in the production tree. |
| **Coverage expectation** | PASS | Pooled 90.81% line / 80.62% branch; new matcher file 100.00% / 89.58%; every instrumented changed line covered; no regression. |
| **Property-based tests (T1 density)** | PASS | `OpenClaw.Core` is T1 (`quality-tiers.yml`); the new pure function (`ChooseMostLikelyRelatedEvent`) has four genuine CsCheck property tests (null-or-score>=4, permutation invariance via seeded Fisher-Yates, case-insensitivity, duplicate set semantics; 1000 iterations each; overlapping generator pools so both accept and reject branches are exercised). CsCheck 4.7.0 was already referenced by `OpenClaw.Core.Tests`; no new dependency. |
| **Mutation testing** | N/A | Mutation testing runs in pre-merge/nightly pipelines per policy, not the per-commit loop (same disposition as the validated #80/#99 T1 audits). |
| **Determinism (no sleeps, no wall clock)** | PASS | `FakeTimeProvider` pinned to a fixed epoch in all fallback/dedupe/worker tests; window bounds asserted as exact `Now`/`Now.AddDays(14)` values; no `Thread.Sleep`/`Task.Delay`/timers in the diff. The candidate-source tests' `DateTimeOffset.UtcNow` anchoring is forced by the production class's pre-existing wall-clock read (unchanged line); documented with generous margins and recorded as a Minor follow-up. |
| **No temporary files** | PASS | In-memory shared-cache SQLite (`Data Source=candidate-source-{Guid:N};Mode=Memory;Cache=Shared`); zero filesystem artifacts. |
| **Focused / isolated** | PASS | Fresh mock graph or fresh in-memory database per test; helper factories build per-test state only; explicit window stubs avoid reliance on Moq loose defaults. |

---

## 5. Test Coverage Detail

### RelatedEventMatcherTests (14 new unit tests, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `Subject_only_match_with_two_shared_tokens_scores_four_and_is_accepted` | Positive (subject-only, score 4) | PASS |
| `Attendee_only_match_with_two_shared_participants_scores_six_and_is_accepted` | Positive (attendee-only, score 6) | PASS |
| `Combined_token_and_participant_scores_five_and_is_accepted` | Positive (combined, score 5) | PASS |
| `Single_participant_match_scores_three_and_returns_null` | Negative (sub-threshold, score 3) | PASS |
| `Exact_threshold_score_of_four_is_accepted` | Boundary (exact threshold) | PASS |
| `Tie_break_selects_earliest_start_and_null_start_sorts_last` | Determinism (Start tie-break) | PASS |
| `Tie_break_selects_smallest_ordinal_id_when_start_ties` | Determinism (Id tie-break) | PASS |
| `Empty_event_list_returns_null` | Edge (empty input) | PASS |
| `Null_subject_contributes_zero_so_single_participant_stays_below_threshold` | Edge (null subject) | PASS |
| `Short_tokens_are_ignored_so_single_participant_stays_below_threshold` | Edge (token length < 4) | PASS |
| `Token_and_email_matching_is_case_insensitive` | Normalization | PASS |
| `Organizer_email_is_not_counted_toward_the_score` | Reference fidelity | PASS |
| `Null_message_throws_argument_null_exception` | Negative (null guard) | PASS |
| `Null_events_throws_argument_null_exception` | Negative (null guard) | PASS |

### RelatedEventMatcherPropertyTests (4 new CsCheck properties, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `Result_is_null_or_the_selected_event_scores_at_least_four` | Property (threshold invariant, 1000 iters) | PASS |
| `Selection_is_invariant_under_permutation_of_the_event_list` | Property (order independence, seeded Fisher-Yates) | PASS |
| `Scoring_is_case_insensitive` | Property (case invariance) | PASS |
| `Duplicated_tokens_and_emails_do_not_change_the_score` | Property (set semantics) | PASS |

### CacheSchedulingCandidateSourceTests (4 new unit tests, new file; in-memory SQLite)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `GetCandidateMessageIds_returns_mail_alongside_meeting_within_window` | Positive (AC-1 widening; fail-before discriminator) | PASS |
| `GetCandidateMessageIds_excludes_rows_older_than_the_lookback_window` | Behavior-preserving (lookback) | PASS |
| `GetCandidateMessageIds_caps_the_result_at_the_configured_limit` | Behavior-preserving (limit) | PASS |
| `GetCandidateMessageIds_preserves_recency_ordering_across_kinds` | Positive (ordering; fail-before discriminator) | PASS |

### SchedulingWorkerFallbackTests (7 new test cases across 6 methods, new file)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `RunCycle_LookupMiss_FetchesCalendarViewFromNowToNowPlusFourteenDays` | Positive (window bounds from FakeTimeProvider; fail-before discriminator) | PASS |
| `RunCycle_WindowEventClearsThreshold_HydratesEventContextIntoSendPath` | Positive (match hydration observable in reply subject; fail-before discriminator) | PASS |
| `RunCycle_EmptyWindow_ProceedsMessageOnlyWithoutThrowing` | Negative (empty window; fail-before discriminator) | PASS |
| `RunCycle_NonPositiveFallbackDays_NeverFetchesCalendarView(0)` / `(-1)` | Edge (documented opt-out, 2 DataRows) | PASS |
| `RunCycle_AllWindowEventsBelowThreshold_FallsThroughToMessageOnly` | Negative (sub-threshold window) | PASS |
| `RunCycle_FailureEnvelopeMappedToEmptyWindow_ProceedsWithoutException` | Error handling (degradation) | PASS |

### SchedulingWorkerDedupeTests (1 new test; shared stubs extended) and SchedulingWorkerTests (shared stub extended)

| Test Name | Scenario Type | Status |
|-----------|--------------|--------|
| `RunCycle_OrdinaryMailAcrossTwoCycles_SendsOnceWithUnchangedKeyShape` | State transitions (AC-6 cross-cycle dedupe, stateful store double, exact key-set assertion) | PASS |
| Existing worker/dedupe suites (explicit empty-window stubs added) | Behavior-preserving | PASS |

**Coverage:** `RelatedEventMatcher.cs` 100.00% line / 89.58% branch (new file, reviewer-parsed); `CacheSchedulingCandidateSource.cs` 100.00% line; `SchedulingWorker.Pipeline.cs` instrumented figures identical to baseline with the async fallback behaviorally covered. **Gap:** none attributable to this branch (the pipeline's two partial branches are pre-existing untouched ternaries; the matcher's five partial condition-arms are nullable-lifted compiler branches — an Info note in the code review suggests a null-Id tie-break row).

**Fail-before / pass-after:** `evidence/regression-testing/expect-fail-candidate-widening.2026-07-02T13-11.md` (EXIT 1; 2 discriminating tests fail pre-change, 2 behavior-preserving tests pass) and `expect-fail-worker-fallback.2026-07-02T13-11.md` (EXIT 1; 3 fallback tests fail pre-change), followed by the green final suite (`final-qa-dotnet-test.2026-07-02T13-21.md` EXIT 0 and the reviewer's 731/731 re-run).

---

## 6. Test Execution Metrics

| Metric | Value | Status |
|--------|-------|--------|
| Total Tests (solution, reviewer run) | 736 (731 passed, 5 env-gated skips) | PASS |
| OpenClaw.Core.Tests | 284 passed / 284 | PASS |
| Tests Failed | 0 | PASS |
| Core.Tests Execution Time | ~1 s | PASS |
| Pooled Code Coverage | 90.81% line, 80.62% branch | PASS |
| New matcher file (T1, new code) | 100.00% line, 89.58% branch | PASS |
| Net new tests vs baseline | +30 (14 matcher unit + 4 matcher property + 4 candidate-source + 7 fallback cases + 1 dedupe) | PASS |

---

## 7. Code Quality Checks

| Check | Command | Result | Status |
|-------|---------|--------|--------|
| CSharpier format | `csharpier check .` (CSharpier 1.3.0, reviewer) | Checked 217 files, EXIT 0 | PASS |
| .NET analyzers + nullable | `dotnet build OpenClaw.MailBridge.sln` | 0 warnings, 0 errors | PASS |
| Architecture (NetArchTest) | Included in `dotnet test` Core.Tests run | 284/284 pass (boundary tests included) | PASS |
| MSTest tests + coverage | `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/qa-gates/coverage-review"` | 731 passed, 0 failed, 5 skipped | PASS |

**Notes:** The reviewer re-ran the full toolchain against branch head `0f346d5` on 2026-07-02. The 5 skips are the same environment-gated COM/publish tests skipped at baseline; none relate to this change.

---

## 8. Gaps and Exceptions

### Identified Gaps

**None Blocking.** Two observations, recorded (not policy violations on this branch):

- **Async-body instrumentation exclusion (Informational).** The pre-existing `mailbridge.runsettings` coverlet setting `ExcludeByAttribute=CompilerGeneratedAttribute` excludes async state-machine bodies from instrumentation solution-wide. The new fallback code (`ChooseRelatedEventFromWindowAsync` and the fallback guard inside async `ProcessMessageAsync`) therefore contributes zero instrumented lines; per-line cobertura cannot attest the changed lines in `SchedulingWorker.Pipeline.cs`. Per the disposition accepted on the #99 review, the reviewer verified the new body behaviorally instead: 7 fallback test cases cover the guard both ways (miss-with-positive-days fetches; non-positive days never fetches; direct-hit paths in the pre-existing worker suites never fetch), the match and no-match arms, and the failure degradation — with fail-before evidence (EXIT 1, the 3 discriminating tests) proving the tests detect the absent implementation. The runsettings file is byte-identical to base on this branch; the setting is an attribute-level filter, not a production-path `exclude` entry of the kind the coverage-exclusion policy prohibits. The recommended runsettings follow-up remains open (also recorded on #99).
- **Wall-clock anchor in `CacheSchedulingCandidateSource` (Minor, pre-existing).** The production class computes `sinceUtc` from `DateTimeOffset.UtcNow` directly (line unchanged by this branch — only the kind literal and doc comment changed). This forces the new tests to seed rows relative to real now instead of a `FakeTimeProvider`. The tests are deterministic in practice (1-4h offsets against a 48h window; the stale row at -100h), but the class should receive an injected `TimeProvider` per the csharp.md clock-seam rule. Recorded as a Minor follow-up recommendation in the code review; refactoring it was outside this feature's spec scope.

### Approved Exceptions

- **CSharpier invocation path:** the repo tool-manifest restore fails in this environment ("command csharpier ... package contains dotnet-csharpier"); the reviewer used the globally installed CSharpier 1.3.0, matching the accommodation recorded in the #70, #80, #19, #18, #99, and #101 audits. The format check ran to EXIT 0 over all 217 files.
- **MCP template/validator tools unavailable:** the MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are not available in this review environment. The artifact structure was reproduced from the most recent validator-passing artifact set (issue #99 review, 2026-07-02) and the recorded validator requirements (exact headings, Coverage Evidence Checklist literals, single-line Section 1.2.1 comparison). Documented best-effort assumption per the workflow's fail-soft guidance.
- **GitHub CLI unavailable:** `gh` is not installed, so issue cross-verification in the PR-context artifacts is author-asserted only. Does not affect any gate in this audit.

### Removed/Skipped Tests

- **None removed.** No existing test was deleted or weakened; the two modified test files gained explicit `GetCalendarViewAsync` empty-window stubs (a strengthening — the spec explicitly required explicit stubs over Moq loose defaults) and one new dedupe test. The 5 solution skips are pre-existing environment-gated COM/publish tests, unchanged.

---

## 9. Summary of Changes

### Commits in This Branch (vs base `3dae644`)

Branch `feature/ordinary-mail-candidates-103`, head `0f346d5e74a5543526ba8e642fe684b73475dba3` (single commit: "feat(core): triage ordinary scheduling mail with calendarView fallback matching"). Range: `3dae644d98dcb564767002a51503e6b9944e4eab..0f346d5e74a5543526ba8e642fe684b73475dba3`.

### Files Modified (categories)

1. **`src/OpenClaw.Core/Agent/RelatedEventMatcher.cs`** (NEW, 191 lines) — pure static Section 9.2 port: `GeneratedRegex` tokenizer, participant/attendee email sets via `MeetingContextNormalizer.EmailOf`, weighted scoring with named constants, threshold-4 acceptance, deterministic tie-break, internal with-score overload for logging.
2. **`src/OpenClaw.Core/Agent/Contracts/AgentPolicyOptions.cs`** (MODIFIED) — `CalendarViewFallbackDays` int property, default 14, XML doc citing master Section 9.2 and the non-positive opt-out.
3. **`src/OpenClaw.Core/Agent/Runtime/CacheSchedulingCandidateSource.cs`** (MODIFIED) — kind literal `"meeting"` -> `"all"`; XML summary updated to the widened candidate set.
4. **`src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs`** (MODIFIED) — guarded fallback block after the direct lookup plus private `ChooseRelatedEventFromWindowAsync` (TimeProvider-derived window, seam fetch, matcher application, LogDebug/LogInformation per spec).
5. **`tests/OpenClaw.Core.Tests/Agent/**`** — 4 new test files (matcher unit, matcher property, candidate source, worker fallback) and 2 extended files (explicit window stubs; ordinary-mail cross-cycle dedupe test).
6. **`docs/features/active/2026-07-02-ordinary-mail-candidates-103/**`** (NEW, 16 files) — issue/spec/user-story/plan and canonical evidence (baseline, qa-gates, regression-testing).
7. **`.claude/agent-memory/orchestrator/**`** (3 files) — orchestrator agent-memory bookkeeping; no code or policy content.

---

## 10. Compliance Verdict

### Overall Status: FULLY COMPLIANT

The C# change passes formatting, linting, nullable type-checking, architecture-boundary enforcement, the full unit-test suite, and the uniform coverage gates, all independently re-run by the reviewer at branch head. The T1 property-test obligation for the new pure function is satisfied with four genuine CsCheck properties. Fail-before/pass-after regression evidence is present and discriminates both delivered behaviors exactly. No evidence-location or file-size violations. No new suppressions or banned-API additions. The `modified-workflow-needs-green-run` rule does not fire (verified: no `.github/workflows/`, `scripts/benchmarks/`, or `.github/actions/` paths in the diff). The `benchmark-baselines` and `ci-workflows` rules are not triggered.

**Fail-closed reminder:** All required baseline and post-change coverage metrics are present and independently re-verified; the audit is marked PASS because no required artifact, metric, or gate is missing or failing.

---

### Policy-by-Policy Summary

#### General Code Change Policy (Section 2)
- Before Making Changes: PASS (spec/plan/policy-order evidence present)
- Design Principles: PASS (one-literal widening, one pure class, one guarded seam call)
- Module & File Structure: PASS (all files under 500 lines, max 364)
- Naming, Docs, Comments: PASS
- Toolchain Execution: PASS (single clean pass, reviewer re-verified)
- Summarize & Document: PASS

#### Language-Specific Code Change Policy (Section 3) — C#
- Tooling & Baseline: PASS
- Design & Type-Safety: PASS
- Error Handling: PASS (fail-fast null guards; no new catch blocks; documented degradation path)

#### General Unit Test Policy (Section 1)
- Core Principles: PASS
- Coverage & Scenarios: PASS (90.81%/80.62% pooled; matcher 100%/89.58%; changed lines covered or behaviorally verified)
- Test Structure: PASS
- External Dependencies: PASS (Moq seams, in-memory SQLite, no temp files)
- Policy Audit: PASS

#### Language-Specific Unit Test Policy (Section 4) — C#
- Framework & Location: PASS (MSTest + FluentAssertions + Moq repo convention; tests/ mirror)
- Determinism: PASS (FakeTimeProvider throughout the pipeline tests; documented wall-clock accommodation for the pre-existing candidate-source anchor)
- T1 obligations: PASS (four CsCheck property tests for the new pure function; mutation gate is pipeline-stage, not per-commit)

---

### Metrics Summary

- 731/731 runnable solution tests passing (5 pre-existing environment-gated skips)
- 90.81% pooled line coverage, 80.62% pooled branch coverage (gates: 85%/75%)
- New T1 matcher file: 100.00% line / 89.58% branch (reviewer-parsed, line and branch)
- No regression: pooled +0.18pp line / +0.37pp branch; every changed file's instrumented figures equal or exceed baseline
- Build: 0 warnings / 0 errors (analyzers + nullable as errors)
- All 10 touched `.cs` files under the 500-line cap (max 364)

---

### Recommendation

**Ready for merge — Go.** All toolchain stages, coverage gates, regression evidence, and policy requirements pass against branch head `0f346d5`. No remediation inputs are required. Operational note (from spec, not a gate): widened candidates increase processed volume within `Defaults.Limit`; the accepted Local-MVP risk of mail crowding out meeting messages is documented with a per-kind quota as a possible follow-up, and all side effects remain behind `SendEnabled`/`CalendarWriteEnabled`.

---

## Appendix A: Test Inventory

C# test changes in this feature (all in `tests/OpenClaw.Core.Tests/Agent/` and `tests/OpenClaw.Core.Tests/Agent/Runtime/`):

1. `RelatedEventMatcherTests.cs` (NEW, 257 lines) — 14 unit tests: scoring rows (subject-only, attendee-only, combined, sub-threshold, exact-threshold), tie-breaks (earliest Start with null-last, ordinal Id), empty events, null/short subjects, case-insensitivity, organizer exclusion, null-arg guards.
2. `RelatedEventMatcherPropertyTests.cs` (NEW, 273 lines) — 4 CsCheck properties (1000 iterations each): null-or-score>=4, permutation invariance (seeded Fisher-Yates), case-insensitivity, duplicate set semantics.
3. `Runtime/CacheSchedulingCandidateSourceTests.cs` (NEW, 175 lines) — 4 tests against a real in-memory shared-cache SQLite `CoreCacheRepository`: mail-alongside-meeting inclusion, lookback exclusion, limit cap, recency ordering across kinds.
4. `Runtime/SchedulingWorkerFallbackTests.cs` (NEW, 312 lines) — 7 test cases: window bounds from `FakeTimeProvider` (now to now+14d), match hydration observable in the outbound reply subject, empty-window message-only fall-through, non-positive opt-out (DataRows 0 and -1), sub-threshold fall-through, failure-envelope degradation.
5. `Runtime/SchedulingWorkerDedupeTests.cs` (MODIFIED) — added `RunCycle_OrdinaryMailAcrossTwoCycles_SendsOnceWithUnchangedKeyShape` (stateful store double; consult-twice/record-once; exact key-set assertion) plus explicit empty-window stubs and a parameterizable `MeetingMessageType`.
6. `Runtime/SchedulingWorkerTests.cs` (MODIFIED) — explicit `GetCalendarViewAsync` empty-window stub added to the shared service factory (spec-required explicitness; no assertions weakened).

Reviewer run: `OpenClaw.Core.Tests` 284 passed, 0 failed; solution total 731 passed, 0 failed, 5 env-gated skipped.

---

## Appendix B: Toolchain Commands Reference (C#)

```bash
# Formatting (reviewer, CSharpier 1.3.0 global — repo tool-manifest accommodation)
csharpier check .

# Lint + nullable type-check + analyzers (as errors per Directory.Build.props)
dotnet build OpenClaw.MailBridge.sln

# Tests + coverage (full solution; reviewer results directory is the canonical evidence path)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-02-ordinary-mail-candidates-103/evidence/qa-gates/coverage-review"

# Regression subsets (executor fail-before evidence)
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~CacheSchedulingCandidateSourceTests"
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --filter "FullyQualifiedName~SchedulingWorkerFallbackTests"

# Evidence-location scan
git diff --name-only 3dae644d98dcb564767002a51503e6b9944e4eab..HEAD | grep -E '^artifacts/'
```

---

**Audit Completed By:** feature-review agent
**Audit Date:** 2026-07-02
**Policy Version:** Current (as of audit date)
