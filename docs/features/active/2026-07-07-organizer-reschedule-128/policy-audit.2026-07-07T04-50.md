# Policy Audit — organizer-reschedule (Issue #128)

- **Feature folder:** `docs/features/active/2026-07-07-organizer-reschedule-128/`
- **Branch:** `feature/organizer-reschedule-128` (HEAD `294d976cb1953c8bf24a40fba17338eb5b35b588`)
- **Base:** `epic/openclaw-vision-integration` (merge-base `68a516f78af252f4ef01109f596285ea92f4952b`, committed 2026-07-07T03:10:57-04:00)
- **Work mode:** `full-feature` (from `issue.md`: `- Work Mode: full-feature`)
- **Reviewed:** 2026-07-07T04-50 UTC
- **Overall verdict: PASS**

## 0. Environment Accommodations

- The MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are
  not available in this review environment; this artifact set mirrors the structure of the most
  recent accepted C# artifact set (`docs/features/active/2026-07-07-graph-activity-log-purview-124/`).
- `dotnet tool restore` is not usable in this environment; the globally installed `csharpier`
  (invoked as `csharpier check .`, confirmed on PATH) was used instead — an accepted fallback.
- `artifacts/pr_context.summary.txt` and `artifacts/pr_context.appendix.txt` were absent at review
  start. Both were regenerated directly from git in this review (summary: branch/head/base,
  merge-base SHA + timestamp, commit list, name-status, diff stat; appendix: full
  `git diff 68a516f..HEAD`), the accepted fallback established on the #120/#124 reviews. No
  repo-local collector script exists.
- `scripts/dev_tools/validate_orchestrator_state.py` does not exist in this repository (confirmed
  by `find`); the `human_interaction` invariants from `.claude/rules/orchestrator-state.md` were
  verified manually against `artifacts/orchestration/orchestrator-state.json` (see section 11).

## 1. Scope Verification

`git diff 68a516f78af252f4ef01109f596285ea92f4952b..HEAD` touches **41 files** (+3707/-9).
Confirmed by direct `git diff --name-status` re-run in this review (not taken from any summary):

- **Production code (10 files):**
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Reschedule.cs` (new, 302 lines),
  `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs` (new, 57 lines),
  and 8 modified: `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` (additive member 10),
  `src/OpenClaw.Core/HostAdapterHttpClient.cs` (fail-closed `NOT_SUPPORTED`),
  `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs`,
  `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs`,
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.cs` (ctor gains `ISeriesMoveHistory`),
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` (threads `meetingEvent`; replaces
  the `!CalendarWriteEnabled` stub), `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs`
  (four new constants), `src/OpenClaw.Core/Agent/SentActionKey.cs` (`OrganizerReschedule` constant).
- **Test code (10 files):** 6 new (`SchedulingWorkerRescheduleTests.cs`,
  `SchedulingWorkerRescheduleEdgeTests.cs`, `SchedulingWorkerRescheduleIntentPropertyTests.cs`,
  `GraphHostAdapterClientRescheduleEventTests.cs`, `HostAdapterHttpClientRescheduleTests.cs`,
  `HostAdapterSchedulingServiceRescheduleTests.cs`) and 4 modified pre-existing worker test files
  (each with exactly one mechanical added line: `new Mock<ISeriesMoveHistory>().Object,` — verified
  in the appendix diff).
- **Feature docs, evidence, runbook, memory (21 files):** under
  `docs/features/active/2026-07-07-organizer-reschedule-128/` (issue/spec/user-story/plan/research,
  `evidence/{baseline,qa-gates,regression-testing,other}/`, `runbooks/`) and `.claude/agent-memory/`.

Languages with changed files on this branch: **C# only.** TypeScript, Python, and PowerShell each
have zero changed files.

**Verdict: PASS.**

### No workflow/benchmark changes

`git diff --name-only 68a516f..HEAD | grep -E '^\.github/|^scripts/benchmarks/'` returns empty
output. **Verdict: PASS.** `modified-workflow-needs-green-run` (`.claude/rules/ci-workflows.md`,
`.claude/rules/benchmark-baselines.md`) does not apply to this branch.

## 2. Rejected Scope Narrowing

None observed. The caller instructions explicitly requested the full feature-vs-base audit against
merge-base `68a516f` and did not attempt to narrow scope to a plan/task/phase subset or mark any
language out of scope. Observation (not a narrowing): the canonical PR-context artifacts had to be
self-generated from git in this review because they were absent; a self-generated summary does not
carry the historical docs-only misclassification quirk.

## 3. Evidence Location Compliance

`git diff --name-only 68a516f..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
returns no matches. No `validate_evidence_locations.py` script exists in this repository (known,
previously-documented environment gap). All committed evidence for this feature lives under the
canonical `docs/features/active/2026-07-07-organizer-reschedule-128/evidence/{baseline,qa-gates,regression-testing,other}/`
tree. This review's own fresh coverage runs were routed to the canonical
`evidence/qa-gates/coverage-review/{settings-mode,plain-mode}/` path via `--results-directory`.
**Verdict: PASS.** No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries required.

## 4. Architecture-Boundary Compliance (No-COM; domain must not depend on adapters)

Independent verification performed in this review:

1. `grep -rn "CloudGraph|Microsoft.Office|ComVisible" src/OpenClaw.Core/Agent/` → the only match is
   a documentation sentence in `SchedulingWorker.Reschedule.cs` line 12 stating the partial
   "references no CloudGraph type". Zero compiled references from the Agent (domain) partition to
   the `CloudGraph` adapter namespace. The worker reaches Graph only through the
   `ISchedulingService` seam; the Graph PATCH lives only in
   `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.RescheduleEvent.cs` behind
   `IHostAdapterClient`.
2. `dotnet build OpenClaw.MailBridge.sln -warnaserror`: **0 Warning(s), 0 Error(s)**.
3. The NetArchTest architecture-boundary suites (`AgentArchitectureBoundaryTests`,
   `CloudGraphArchitectureBoundaryTests`, `CloudAuthArchitectureBoundaryTests`,
   `CloudSyncArchitectureBoundaryTests`, `ScopeValidationArchitectureBoundaryTests`) are part of
   `OpenClaw.Core.Tests` and passed in this review's full run (893/893). These suites assert the
   deterministic surface does not depend on the bridge, the `OpenClaw.HostAdapter` host
   implementation, or COM interop; `OpenClaw.HostAdapter.Contracts` (where the additive
   `IHostAdapterClient` member lives) is an explicitly allowed dependency of the runtime seam.
4. No `Microsoft.Office.Interop`, VSTO, or `[ComVisible(true)]` additions anywhere in the diff
   (appendix grep). Mailbox/calendar data access remains behind the Graph adapter seam.

**Verdict: PASS.** Existing NetArchTest boundary tests pass unmodified, as the spec requires.

## 5. Test Coverage Detail

Coverage was independently re-measured in this review in **both instrumentation modes** (fresh
runs, not the executor's committed artifacts), then compared against the executor's committed
baseline (`artifacts/csharp/baseline/*/coverage.cobertura.xml`, captured 2026-07-07 04:03, i.e.
after the merge-base commit at 03:10 — the baseline is fresh, no stale-baseline hazard):

- Convention mode (`dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings
  --collect:"XPlat Code Coverage"`), baseline-comparable: **1340 passed, 5 skipped, 0 failed**
  (Core 893, HostAdapter 100, MailBridge 347+5 pre-existing COM/publish skips).
  `OpenClaw.Core` package: **line 99.27%, branch 92.24%** — exactly matching the executor's
  committed `evidence/qa-gates/csharp-test-coverage.2026-07-07T04-01.md`.
- Plain mode (`dotnet test tests/OpenClaw.Core.Tests --collect:"XPlat Code Coverage"`, no
  `--settings`), which fully instruments async bodies: 893/893 passed. `OpenClaw.Core` package:
  **line 93.33%, branch 81.55%.**

Per-language comparison (C# is the only language with changed files):

- **C#:** Baseline: 99.25% line / 92.21% branch (OpenClaw.Core, settings mode) -> Post-change: 99.27% line / 92.24% branch. Change: +0.02% line / +0.03% branch. New/changed-code coverage: 100% line on every changed production file (settings mode); plain-mode full-instrumentation per-file minimum 92.57% line / 75.93% branch, new files 100% line. Disposition: PASS — both uniform thresholds (line >= 85%, branch >= 75%) held repo-wide and per changed file, no regression on changed lines. Evidence: `evidence/qa-gates/coverage-review/{settings-mode,plain-mode}/` (this review's runs) and `evidence/qa-gates/coverage-comparison.2026-07-07T04-01.md`.
- **TypeScript:** N/A — zero changed files on this branch.
- **Python:** N/A — zero changed files on this branch.
- **PowerShell:** N/A — zero changed files on this branch.

Per-file measurements (this review's own cobertura parse, duplicate `<class>` entries pooled per
line; both modes):

| File | Settings mode (line / branch) | Plain mode (line / branch) | Verdict |
|---|---|---|---|
| SchedulingWorker.Reschedule.cs (new) | 100% (38/38) / 92.86% (13/14) | 100% (191/191) / 95.83% (23/24) | PASS |
| GraphHostAdapterClient.RescheduleEvent.cs (new) | 100% (21/21) / no conditions | 100% (21/21) / no conditions | PASS |
| HostAdapterSchedulingService.cs (mod) | 100% / no conditions | 95.70% / 75.93% (41/54) | PASS |
| SchedulingWorker.Pipeline.cs (mod) | 100% / 50% (2/4, unchanged from baseline) | 94.07% / 77.78% | PASS |
| SchedulingWorker.cs (mod) | 100% / no conditions | 96.61% / 100% | PASS |
| HostAdapterHttpClient.cs (mod) | 100% / 100% (2/2) | 92.57% / 85.71% | PASS |
| SentActionKey.cs (mod) | 100% / 100% | 100% / 100% | PASS |
| ActionAuditResultCode.cs (mod) | const-only, not instrumented | const-only, not instrumented | PASS (permitted omission) |
| ISchedulingService.cs (mod) | interface-only, not instrumented | interface-only, not instrumented | PASS (permitted omission) |
| IHostAdapterClient.cs (mod) | interface-only, not instrumented | interface-only, not instrumented | PASS (permitted omission) |

Changed-line no-regression verification:

- `SchedulingWorker.Pipeline.cs` settings-mode 50% branch is **identical to baseline**: the two
  partial-condition lines at head (306, 313) are the same two pre-existing ternaries the baseline
  reports at lines 298, 305 — shifted +8 by the added lines (confirmed by parsing the committed
  baseline cobertura and mapping line numbers, the #103 technique). The changed lines themselves
  (added parameter at line 70, the `EvaluateRescheduleAsync` call at 291-299) carry full line
  coverage and no conditions.
- All plain-mode missed lines in modified files lie **outside the changed line ranges**
  (`HostAdapterSchedulingService.cs` 37-45 pre-existing `GetEventForMessageAsync`;
  `SchedulingWorker.cs` 45/50 pre-existing `ExecuteAsync` delay-loop plumbing;
  `HostAdapterHttpClient.cs` 21-25 TokenReader default and 226-234 pre-existing `PostAsync`;
  `Pipeline.cs` 20-22/56-57/151-159 pre-existing hydration-miss/gate/NotSupported paths) —
  verified by mapping missed line numbers against the appendix diff hunks.

Async-instrumentation disclosure: the `mailbridge.runsettings` `ExcludeByAttribute`
(CompilerGenerated) setting excludes async method bodies from settings-mode instrumentation, so
the settings-mode per-file figures under-count the new async orchestration body. The plain-mode
run closes that gap: the entire new `EvaluateRescheduleAsync` body is measured
(191 instrumented lines, 100% covered, 23/24 branches). No coverage masking applies to this
review's verdict.

**Coverage Evidence Checklist:**

- C# baseline artifact: `artifacts/csharp/baseline/*/coverage.cobertura.xml` + `evidence/baseline/csharp-test-coverage.2026-07-07T04-01.md` — present.
- C# post-change artifact: `evidence/qa-gates/coverage-review/settings-mode/` and `plain-mode/` (this review) + `artifacts/csharp/final/` (executor) — present.
- TypeScript baseline artifact: N/A - out of scope (zero changed TypeScript files).
- TypeScript post-change artifact: N/A - out of scope (zero changed TypeScript files).
- PowerShell baseline artifact: N/A - out of scope (zero changed PowerShell files).
- PowerShell post-change artifact: N/A - out of scope (zero changed PowerShell files).

**Coverage Verdict: PASS** (explicit PASS for C#, the only language with changed files).

## 6. Toolchain Compliance (`general-code-change.md` seven-stage loop)

Independently re-run in this review:

| Stage | Result | Evidence |
|---|---|---|
| 1. Formatting (CSharpier) | PASS | `csharpier check .` — "Checked 367 files in 519ms", zero unformatted |
| 2. Linting (.NET analyzers) | PASS | `dotnet build OpenClaw.MailBridge.sln -warnaserror`: 0 Warning(s), 0 Error(s) |
| 3. Nullable type-check | PASS | same build (`Nullable=enable`, `TreatWarningsAsErrors=true`) |
| 4. Architecture-boundary tests | PASS | NetArchTest suites within the 893/893 Core.Tests pass (section 4) |
| 5. Unit tests (incl. property tests) | PASS | 1340 passed, 5 pre-existing skips, 0 failed |
| 6. Contract/schema compatibility | PASS | additive-only contract change verified: `IHostAdapterClient` member 10 and `ISchedulingService.RescheduleEventAsync` are appended members with defaults; no existing member signature changed (appendix diff); mocked-Graph wire-contract suite asserts the PATCH body structurally (section 9 of code review) |
| 7. Integration tests | N/A | no live external-system integration by design — live-tenant verification is the recorded HI-1 human-interaction exception (section 11) |

**Verdict: PASS — single clean pass reproduced from fresh invocations in this review.**

## 7. Code Quality Checks

See `code-review.2026-07-07T04-50.md` for the full narrative review. Policy-relevant checks
performed directly in this audit:

- **File size limit (<= 500 lines):** all 10 production and 6 new test files measured by `wc -l`;
  maximum production file 331 lines (`SchedulingWorker.Pipeline.cs`), maximum test file 412 lines
  (`SchedulingWorkerRescheduleTests.cs`). **PASS.**
- **Banned APIs** (`DateTime.Now`, `DateTime.UtcNow`, `Random.Shared`, `Thread.Sleep`,
  `Task.Delay`, wall-clock waits): zero matches across the added/changed production and test
  files; all time flows through the injected `TimeProvider` (`FakeTimeProvider` in tests); the
  429-exhaustion contract test advances simulated time in a bounded loop. **PASS.**
- **Untyped escape hatches (`dynamic`):** zero occurrences in added lines (T1 threshold 0). **PASS.**
- **Suppressions** (`#pragma warning disable`, `SuppressMessage`, `NOSONAR`): zero occurrences in
  added lines. **PASS.**
- **No new analyzer or nullable suppressions:** confirmed by grep over the appendix diff added lines. **PASS.**
- **Temp-file prohibition in tests:** zero `GetTempFileName`/`GetTempPath`/file-write matches in
  the six added test files (re-confirming `evidence/qa-gates/test-hygiene.2026-07-07T04-01.md`). **PASS.**
- **Test file location:** all six new test files live under `tests/OpenClaw.Core.Tests/` mirroring
  the production tree; no colocation. **PASS.**

## 8. Property-Test-Density Gate (T1, `quality-tiers.md`)

`OpenClaw.Core` is T1 (`quality-tiers.yml` line 13). The feature introduces two new pure functions:
`SchedulingWorker.ComputeRescheduleIntent` and `SchedulingWorker.BuildRescheduleActingFlags`.
`SchedulingWorkerRescheduleIntentPropertyTests.cs` contains three genuine CsCheck property tests
(1000 samples each): duration-preservation and missing-precondition-never-yields-intent for the
intent helper, and a full round-trip property for the flags snapshot. Both pure functions carry at
least one property test. **Verdict: PASS.**

Mutation testing (T1, >= 75%) runs in the pre-merge/nightly pipeline per policy, not in this
per-commit review; the truth-table and fail-closed branches are covered by directed tests that
assert both the action taken and the actions not taken (`Times.Never` verifications), which is the
mutation-sensitive surface the spec names.

## 9. Fail-Closed Calendar-Write Gate Verification

The first-real-calendar-write constraint ("any ambiguity resolves to no write") was verified
against source and tests:

- Gate composition: `CalendarWritePolicy.OrganizerRescheduleAllowed` =
  `CalendarWriteEnabled && EnableOrganizerReschedule`, both defaulting `false` (read directly from
  `src/OpenClaw.Core/Agent/CalendarWritePolicy.cs`).
- Evaluation order in `SchedulingWorker.Reschedule.cs` matches the spec exactly: intent →
  move-guard (before the flag gate) → flag gate → dedupe → write → audit-first bookkeeping.
- Every no-write path is asserted with `Times.Never` verifications on
  `RescheduleEventAsync`/`RecordMoveAsync`/`RecordAsync`: three disabled truth-table rows, guard
  block, dedupe hit, five no-intent rows, and the write-failure path (no move-history or dedupe
  row after `reschedule_failed`).
- The local Stage-0 adapter fails closed with a synthesized non-retryable `NOT_SUPPORTED` envelope
  and provably zero HTTP I/O and zero token reads (a throwing `TokenReader` and a counting handler
  in `HostAdapterHttpClientRescheduleTests`).

**Verdict: PASS.**

## 10. Acceptance-Criteria Source Verification (full-feature: `spec.md` + `user-story.md`)

See `feature-audit.2026-07-07T04-50.md` for the full evaluation table. Summary: all 9 checkbox
acceptance criteria (AC-1..AC-9, identical text in both `spec.md` and `user-story.md`) are
independently verified **PASS**. All 18 checkboxes (9 per file) were already checked `[x]` by the
executor; this review confirms each check-off is evidence-backed, so no source-file edits were
required.

## 11. Human-Interaction Exception (orchestrator-state invariants)

`artifacts/orchestration/orchestrator-state.json` contains a `human_interaction` block verified
manually against the three invariants in `.claude/rules/orchestrator-state.md`:

1. `human_interaction` is an object containing a `requirements` list — **holds** (one entry, HI-1).
2. The requirement's `response` is within the `{scope_change, exception, halt}` enum — **holds**
   (`"exception"`).
3. An `exception` response carries a non-empty `runbook_path` — **holds**
   (`docs/features/active/2026-07-07-organizer-reschedule-128/runbooks/organizer-reschedule-live-verification.runbook.md`,
   and the file exists on disk with prerequisites, flag-enablement, observation, and flag-disable
   procedures).

**Verdict: PASS.** (The Python validator script does not exist in this repository; manual
verification documented per section 0.)

## Appendix A: Test Inventory

| Test file | Scope | Result |
|---|---|---|
| `SchedulingWorkerRescheduleTests.cs` | truth table (a: 3 disabled rows + both-on), dry-run detail (b), guard-before-gate (c) | pass |
| `SchedulingWorkerRescheduleEdgeTests.cs` | success ordering (d), failure fail-closed (e), dedupe (f), 5 no-intent rows (g), send-path ActingFlags isolation (h) | pass |
| `SchedulingWorkerRescheduleIntentPropertyTests.cs` | 3 CsCheck properties x 1000 samples | pass |
| `GraphHostAdapterClientRescheduleEventTests.cs` | method/route (a), headers (b), exact body + guardrail (c), 200 mapping (d), D5 400/404/403 (e), 429 exhaustion under FakeTimeProvider (f), unparseable-2xx/mapping-gap (g) | pass |
| `HostAdapterHttpClientRescheduleTests.cs` | NOT_SUPPORTED, zero HTTP I/O, zero token reads, null-requestId self-generation | pass |
| `HostAdapterSchedulingServiceRescheduleTests.cs` | delegation, correlation-id forwarding, failure-throw, id guard | pass |
| 4 modified pre-existing worker test files | mechanical `ISeriesMoveHistory` mock only | pass |
| Full solution (`OpenClaw.Core.Tests`) | 893 | 893/893 pass |
| Full solution (`OpenClaw.HostAdapter.Tests`) | 100 | 100/100 pass |
| Full solution (`OpenClaw.MailBridge.Tests`) | 352 | 347 pass, 5 pre-existing COM/publish skips |

## Appendix B: Command Reference

```
git diff --name-status 68a516f78af252f4ef01109f596285ea92f4952b..HEAD
git diff 68a516f78af252f4ef01109f596285ea92f4952b..HEAD > artifacts/pr_context.appendix.txt
csharpier check .
dotnet build OpenClaw.MailBridge.sln -warnaserror
dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/coverage-review/settings-mode"
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-07-organizer-reschedule-128/evidence/qa-gates/coverage-review/plain-mode"
python parse_cov.py <mode> "<results>/**/coverage.cobertura.xml"   (scratch per-file cobertura parser)
git show -s --format=%cI 68a516f78af252f4ef01109f596285ea92f4952b   (baseline-freshness check)
grep -rn "CloudGraph|Microsoft.Office|ComVisible" src/OpenClaw.Core/Agent/
git diff --name-only 68a516f..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'
git diff --name-only 68a516f..HEAD | grep -E '^\.github/|^scripts/benchmarks/'
```

## Summary

Zero blocking findings. All uniform T1-T4 gates (format, lint, nullable, architecture, line/branch
coverage, changed-line no-regression) hold, verified by fresh independent runs whose figures match
the executor's committed evidence exactly. The fail-closed calendar-write gate, the
guard-before-gate ordering, the additive-only contract change, the property-test-density gate, and
the AC-9 human-interaction exception record all pass. See `code-review.2026-07-07T04-50.md` for
two Minor and two Info non-blocking observations.
