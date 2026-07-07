# Policy Audit — attendee-propose-new-time (Issue #130)

- **Feature folder:** `docs/features/active/2026-07-07-attendee-propose-new-time-130/`
- **Branch:** `feature/attendee-propose-new-time-130` (HEAD `1633a6c5645d7ba3905bf134c06f78b2c8cc169e`, committed 2026-07-07T06:18:09-04:00)
- **Base:** `epic/openclaw-vision-integration` (merge-base `273c7df25d3a5a0cd928a47e8c80fced592ce06b`, committed 2026-07-07T05:07:21-04:00)
- **Work mode:** `full-feature` (from `issue.md`: `- Work Mode: full-feature`)
- **Reviewed:** 2026-07-07T06-35 UTC
- **Overall verdict: PASS**

## 0. Environment Accommodations

- The MCP tools `resolve_policy_audit_template_asset` and `validate_orchestration_artifacts` are
  not available in this review environment; this artifact set mirrors the structure of the most
  recent accepted C# artifact set (`docs/features/active/2026-07-07-organizer-reschedule-128/`, the
  F18 sibling of this F19 attendee path).
- `dotnet tool restore` is not usable in this environment; the globally installed `csharpier`
  version 1.3.0 (invoked as `csharpier check <files>`, confirmed on PATH) was used instead — an
  accepted fallback.
- `scripts/dev_tools/validate_orchestrator_state.py` and `validate_evidence_locations.py` do not
  exist in this repository (confirmed by `find`); the `human_interaction` invariants from
  `.claude/rules/orchestrator-state.md` were verified manually against
  `artifacts/orchestration/orchestrator-state.json` (see section 11) and the evidence-location scan
  was performed with `git diff --name-only` filtered by the canonical non-evidence prefixes.

## 1. Scope Verification

`git diff 273c7df25d3a5a0cd928a47e8c80fced592ce06b..HEAD` touches **37 files** (+3824/-0).
Confirmed by direct `git diff --name-status` re-run in this review (not taken from any summary):

- **Production code (9 files):** 2 new —
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs` (255 lines),
  `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs` (67 lines) — and 7
  modified: `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` (additive member 11),
  `src/OpenClaw.Core/HostAdapterHttpClient.cs` (fail-closed `NOT_SUPPORTED`, no I/O),
  `src/OpenClaw.Core/Agent/Contracts/ISchedulingService.cs` (add `ProposeNewMeetingTimeAsync`),
  `src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs` (implement it),
  `src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.Pipeline.cs` (one `EvaluateProposeNewTimeAsync`
  call after the F18 evaluation), `src/OpenClaw.Core/Agent/Contracts/ActionAuditResultCode.cs`
  (three new constants), `src/OpenClaw.Core/Agent/SentActionKey.cs` (`AttendeeProposeNewTime`
  constant).
- **Test code (6 files, all new):** `SchedulingWorkerProposeNewTimeTests.cs`,
  `SchedulingWorkerProposeNewTimeEdgeTests.cs`,
  `SchedulingWorkerProposeNewTimeIntentPropertyTests.cs`,
  `GraphHostAdapterClientProposeNewTimeTests.cs`, `HostAdapterHttpClientProposeNewTimeTests.cs`,
  `HostAdapterSchedulingServiceProposeNewTimeTests.cs`. **No pre-existing test file was modified**
  (the spec's loose-Moq-mock absorption held: `git diff --name-status -- tests/` shows only `A`
  entries).
- **Feature docs, evidence, runbook, memory (22 files):** under
  `docs/features/active/2026-07-07-attendee-propose-new-time-130/`
  (issue/spec/user-story/plan/research, `evidence/{baseline,qa-gates,regression-testing,other}/`,
  `runbooks/`) and `.claude/agent-memory/atomic-planner/`.

Languages with changed files on this branch: **C# only.** TypeScript, Python, and PowerShell each
have zero changed files.

**Verdict: PASS.**

### No workflow/benchmark changes

`git diff --name-only 273c7df..HEAD | grep -E '^(\.github/workflows/|scripts/benchmarks/|\.github/actions/)'`
returns empty output. **Verdict: PASS.** The `modified-workflow-needs-green-run` rule
(`.claude/skills/feature-review-workflow/SKILL.md`, `.claude/rules/ci-workflows.md`,
`.claude/rules/benchmark-baselines.md`) does not apply to this branch.

## 2. Rejected Scope Narrowing

No caller instruction attempted to narrow scope to a plan/task/phase subset or to mark any
language out of scope; the delegation requested the full feature-vs-base audit against merge-base
`273c7df`. Proceeded with the full audit.

Two non-narrowing observations recorded verbatim per the scope invariant:

- **PR-context summary misclassification (10th recurrence of the known quirk).**
  `artifacts/pr_context.summary.txt` "Changed files overview" reports `Core logic changes: 0 files`
  and `Docs/templates/agents/tooling: 20 files`, listing only markdown files. The authoritative
  `git diff --stat 273c7df..HEAD` contains 9 C# production files and 6 C# test files. Scope was
  taken from the git diff, not the summary's file categorization, per the established disposition on
  the #99/#101/#103/#105/#107/#109/#113/#115/#117 reviews.
- **Autoclose-list noise.** The summary's author-asserted autoclose list contains non-issue tokens
  parsed from AC labels and spec prose (`#AC-1`..`#AC-9`, `#HI-1`, `#ISO-8601`) plus
  design-precedent citations of real but non-closing issues (`#107`, `#109`, `#119`, `#128`). Only
  `#130` is the branch's closing issue. Noise, noted and ignored.

## 3. Evidence Location Compliance

`git diff --name-only 273c7df..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`
returns no matches. All committed evidence for this feature lives under the canonical
`docs/features/active/2026-07-07-attendee-propose-new-time-130/evidence/{baseline,qa-gates,regression-testing,other}/`
tree. This review's own fresh plain-mode coverage run was routed to the canonical
`evidence/qa-gates/coverage-review/plain-mode/` path via `--results-directory`. **Verdict: PASS.**
No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` entries required.

## 4. Architecture-Boundary Compliance (No-COM; domain must not depend on adapters)

Independent verification performed in this review:

1. `grep -rn "CloudGraph|Microsoft.Office|ComVisible" src/OpenClaw.Core/Agent/` → the only matches
   in the new worker partial are documentation sentences stating the partial "references no
   `CloudGraph` type" and reaches Graph only through the `ISchedulingService` seam. Zero compiled
   references from the Agent (domain) partition to the `CloudGraph` adapter namespace. The Graph
   POST lives only in `src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs` behind
   `IHostAdapterClient`.
2. The solution built under `TreatWarningsAsErrors=true` as part of this review's `dotnet test`
   run (the test project transitively builds `OpenClaw.Core`): 0 warnings, 0 errors.
3. The NetArchTest architecture-boundary suites are part of `OpenClaw.Core.Tests` and passed
   unmodified in this review's fresh run (930/930). No architecture-boundary test file appears in
   the diff.
4. No `Microsoft.Office.Interop`, VSTO, or `[ComVisible(true)]` additions anywhere in the diff.
   Calendar data access remains behind the Graph adapter seam.

**Verdict: PASS.** Existing NetArchTest boundary tests pass unmodified, as the spec requires.

## 5. Test Coverage Detail

Coverage was verified from the executor's committed settings-mode Cobertura
(`artifacts/csharp/final-2026-07-07T05-56/`, parsed per-file in this review) and independently
re-measured in **plain instrumentation mode** by a fresh `dotnet test tests/OpenClaw.Core.Tests`
run in this review. The executor's baseline (`evidence/baseline/csharp-test-coverage.2026-07-07T05-56.md`,
captured after the merge-base commit at 05:07 — no stale-baseline hazard) anchors the delta.

- Convention mode (`dotnet test ... --settings mailbridge.runsettings`), baseline-comparable:
  `OpenClaw.Core` package **line 99.29%, branch 92.28%** (executor final run; parsed from
  `bb3efd5f-.../coverage.cobertura.xml` in this review). Baseline was 99.27% / 92.24%.
- Plain mode (`dotnet test tests/OpenClaw.Core.Tests --collect:"XPlat Code Coverage"`, no
  `--settings`), which fully instruments async bodies: **930/930 passed.** The new async
  orchestration body is fully measured (see per-file table).

Per-language comparison (C# is the only language with changed files):

- **C#:** Baseline: 99.27% line / 92.24% branch (OpenClaw.Core, settings mode) -> Post-change: 99.29% line / 92.28% branch. Change: +0.02% line / +0.04% branch. New/changed-code coverage: 100% line on every changed production file; plain-mode full-instrumentation attests the new async worker body at 100% line / 93.75% branch and the new Graph member at 100% line / 100% branch. Disposition: PASS — both uniform thresholds (line >= 85%, branch >= 75%) held repo-wide and per changed file, no regression on changed lines. Evidence: `evidence/qa-gates/coverage-review/plain-mode/` (this review's plain run), `artifacts/csharp/final-2026-07-07T05-56/` (executor settings run), and `evidence/qa-gates/coverage-comparison.2026-07-07T05-56.md`.
- **TypeScript:** N/A — zero changed files on this branch.
- **Python:** N/A — zero changed files on this branch.
- **PowerShell:** N/A — zero changed files on this branch.

Per-file measurements (this review's own cobertura parse, duplicate `<class>` entries pooled per
line; settings figures from the executor final run, plain figures from this review's run):

| File | Settings mode (line / branch) | Plain mode (line / branch) | Verdict |
|---|---|---|---|
| SchedulingWorker.ProposeNewTime.cs (new) | 100% (sync helpers only; async body excluded) / 93.75% | 100% (145/145, async body included) / 93.75% (15/16) | PASS |
| GraphHostAdapterClient.ProposeNewTime.cs (new) | 100% / 100% | 100% (29/29) / 100% | PASS |
| HostAdapterHttpClient.cs (mod, new method) | 100% / 100% | 100% on new method (misses 21-25/258-266 pre-existing) | PASS |
| HostAdapterSchedulingService.cs (mod, new method) | 100% (sync-visible) / 100% | 100% on new method 180-212 (misses 37-45 pre-existing) | PASS |
| SchedulingWorker.Pipeline.cs (mod, 1 added await) | 100% added line / no new branch | 100% on added call (misses pre-existing hydration/gate lines) | PASS |
| SentActionKey.cs (mod) | const-only, not instrumented | const-only, not instrumented | PASS (permitted omission) |
| ActionAuditResultCode.cs (mod) | const-only, not instrumented | const-only, not instrumented | PASS (permitted omission) |
| ISchedulingService.cs (mod) | interface-only, not instrumented | interface-only, not instrumented | PASS (permitted omission) |
| IHostAdapterClient.cs (mod) | interface-only, not instrumented | interface-only, not instrumented | PASS (permitted omission) |

Changed-line no-regression verification:

- All plain-mode missed lines in the modified files lie **outside the changed line ranges**:
  `HostAdapterSchedulingService.cs` 37-45 is the pre-existing lookup/other method (the new
  `ProposeNewMeetingTimeAsync` at 180-212 is fully hit); `HostAdapterHttpClient.cs` 21-25 and
  258-266 are the pre-existing `TokenReader` default and `PostAsync` body (the new fail-closed
  method 186-216 is fully hit); `SchedulingWorker.Pipeline.cs` misses are pre-existing
  hydration-miss/gate lines (the added `EvaluateProposeNewTimeAsync` call is hit) — verified by
  mapping missed line numbers against the appendix diff hunks.
- Package coverage increased vs baseline (+0.02% line / +0.04% branch); no regression at any level.

Async-instrumentation disclosure: the `mailbridge.runsettings` `ExcludeByAttribute`
(CompilerGenerated) setting excludes async method bodies from settings-mode instrumentation. This
review confirmed the effect directly: under settings mode `SchedulingWorker.ProposeNewTime.cs`
instruments only lines 47-115 (the sync helpers `ComputeProposeNewTimeIntent`,
`BuildProposeNewTimeActingFlags`, `BuildProposeNewTimeAuditRecord`), and the async
`EvaluateProposeNewTimeAsync` body (124-254) is absent; likewise the new async
`HostAdapterSchedulingService.ProposeNewMeetingTimeAsync` body is absent. The executor's committed
settings-mode figure for the new worker file ("100% line / 93.75% branch") is therefore accurate
for the mode it names but attests only the sync sub-portion. The plain-mode run closes that gap:
the entire async body is measured (145 instrumented lines, 100% covered). No coverage masking
applies to this review's verdict. The two new Graph/HTTP members and the service-seam new method
are synchronous `Task`-returning methods (no `async`/`await`), so they are fully instrumented in
both modes.

**Coverage Evidence Checklist:**

- C# baseline artifact: `evidence/baseline/csharp-test-coverage.2026-07-07T05-56.md` +
  `artifacts/csharp/baseline-2026-07-07T05-56/*/coverage.cobertura.xml` — present.
- C# post-change artifact: `evidence/qa-gates/csharp-test-coverage.2026-07-07T05-56.md` +
  `artifacts/csharp/final-2026-07-07T05-56/` (executor settings) and
  `evidence/qa-gates/coverage-review/plain-mode/` (this review's plain run) — present.
- TypeScript baseline artifact: N/A - out of scope (zero changed TypeScript files).
- TypeScript post-change artifact: N/A - out of scope (zero changed TypeScript files).
- PowerShell baseline artifact: N/A - out of scope (zero changed PowerShell files).
- PowerShell post-change artifact: N/A - out of scope (zero changed PowerShell files).

**Coverage Verdict: PASS** (explicit PASS for C#, the only language with changed files).

## 6. Toolchain Compliance (`general-code-change.md` seven-stage loop)

Independently re-run in this review:

| Stage | Result | Evidence |
|---|---|---|
| 1. Formatting (CSharpier 1.3.0) | PASS | `csharpier check` over the five changed non-const/interface C# files — "Checked 5 files", zero unformatted (exit 0) |
| 2. Linting (.NET analyzers) | PASS | solution built under `TreatWarningsAsErrors=true` during this review's `dotnet test`: 0 warnings, 0 errors |
| 3. Nullable type-check | PASS | same build (`Nullable=enable`, `TreatWarningsAsErrors=true`) |
| 4. Architecture-boundary tests | PASS | NetArchTest suites within the 930/930 Core.Tests pass (section 4) |
| 5. Unit tests (incl. property tests) | PASS | this review's fresh run: 930/930 OpenClaw.Core.Tests; executor final run 1377 passed, 5 pre-existing COM/publish skips, 0 failed |
| 6. Contract/schema compatibility | PASS | additive-only contract change verified: `IHostAdapterClient` member 11 and `ISchedulingService.ProposeNewMeetingTimeAsync` are appended members with defaults; no existing member signature changed (appendix diff); the mocked-Graph wire-contract suite asserts the POST body structurally |
| 7. Integration tests | N/A | no live external-system integration by design — live-tenant verification is the recorded HI-1 human-interaction exception (section 11) |

**Verdict: PASS — single clean pass reproduced from fresh invocations in this review.**

## 7. Code Quality Checks

See `code-review.2026-07-07T06-35.md` for the full narrative review. Policy-relevant checks
performed directly in this audit:

- **File size limit (<= 500 lines):** all 9 production and 6 new test files measured by `wc -l`;
  maximum production file 255 lines (`SchedulingWorker.ProposeNewTime.cs`), maximum test file 352
  lines (`GraphHostAdapterClientProposeNewTimeTests.cs`). **PASS.**
- **Banned APIs** (`DateTime.Now`, `DateTime.UtcNow`, `Random.Shared`, `Thread.Sleep`,
  `Task.Delay`, wall-clock waits): zero matches across the added/changed production and test files;
  all time flows through the injected `TimeProvider` (`FakeTimeProvider` in tests); the
  429-exhaustion contract test advances simulated time. **PASS.**
- **Untyped escape hatches (`dynamic`):** zero occurrences in added lines (T1 threshold 0). **PASS.**
- **Suppressions** (`#pragma warning disable`, `SuppressMessage`, `NOSONAR`): zero occurrences in
  added production lines. **PASS.**
- **No new analyzer or nullable suppressions:** confirmed by grep over the added lines. **PASS.**
- **Temp-file prohibition in tests:** zero temp-path/file-write matches in the six added test files
  (re-confirming `evidence/qa-gates/test-hygiene.2026-07-07T05-56.md`). **PASS.**
- **Test file location:** all six new test files live under `tests/OpenClaw.Core.Tests/` mirroring
  the production tree; no colocation. **PASS.**

## 8. Property-Test-Density Gate (T1, `quality-tiers.md`)

`OpenClaw.Core` is T1. The feature introduces two new pure functions:
`SchedulingWorker.ComputeProposeNewTimeIntent` and `SchedulingWorker.BuildProposeNewTimeActingFlags`.
`SchedulingWorkerProposeNewTimeIntentPropertyTests.cs` contains three genuine CsCheck property
tests (1000 samples each, failing seed printed on `Sample` failure): duration-preservation and
missing-precondition-never-yields-intent for the intent helper, and a full round-trip property for
the flags snapshot. Both pure functions carry at least one property test. **Verdict: PASS.**

Mutation testing (T1, >= 75%) runs in the pre-merge/nightly pipeline per policy, not in this
per-commit review; the truth-table and fail-closed branches are covered by directed tests that
assert both the action taken and the actions not taken (`Times.Never` verifications on the write,
`RecordMoveAsync`, and the dedupe record), which is the mutation-sensitive surface the spec names.

## 9. Fail-Closed Calendar-Write Gate Verification

The attendee calendar-write constraint ("any ambiguity resolves to no write") was verified against
source and tests:

- Gate composition: `CalendarWritePolicy.AttendeeProposeNewTimeAllowed` =
  `CalendarWriteEnabled && EnableAttendeeProposeNewTime`, both defaulting `false` (F12/#109
  scaffolding, unchanged by this branch).
- Evaluation order in `SchedulingWorker.ProposeNewTime.cs` matches the spec exactly: intent ->
  flag gate -> dedupe -> write -> audit-first bookkeeping. There is **no** move-guard step, **no**
  blocked result code, and **no** `series_moves`/`ISeriesMoveHistory` interaction in any branch —
  confirmed by grep (the only `RecordMoveAsync`/`series_moves`/`ISeriesMoveHistory` tokens in the
  new worker file are documentation comments) and by `Times.Never` `RecordMoveAsync` assertions on
  the disabled, success, and failure paths.
- Mutual exclusivity with the F18 organizer path is by intent predicate
  (`ComputeRescheduleIntent` requires `IsOrganizer == true`; `ComputeProposeNewTimeIntent` requires
  `IsOrganizer == false`), not pipeline branching — asserted in both directions by worker tests.
- The local Stage-0 adapter fails closed with a synthesized non-retryable `NOT_SUPPORTED` envelope
  and provably zero HTTP I/O and zero token reads.

**Verdict: PASS.**

## 10. Acceptance-Criteria Source Verification (full-feature: `spec.md` + `user-story.md`)

See `feature-audit.2026-07-07T06-35.md` for the full evaluation table. Summary: all 9 checkbox
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
   (`docs/features/active/2026-07-07-attendee-propose-new-time-130/runbooks/attendee-propose-new-time-live-verification.runbook.md`,
   and the file exists on disk).

**Verdict: PASS.** (The Python validator script does not exist in this repository; manual
verification documented per section 0.)

## Appendix A: Test Inventory

| Test file | Scope | Result |
|---|---|---|
| `SchedulingWorkerProposeNewTimeTests.cs` | gate truth table (3 disabled rows + both-on writes once), dry-run detail (four time columns, duration, propose ActingFlags), success ordering (audit -> dedupe, no move), failure fail-closed (no dedupe, no move) | pass |
| `SchedulingWorkerProposeNewTimeEdgeTests.cs` | eligibility fail-closed matrix, dedupe skip, mutual exclusivity with the F18 path (both directions) | pass |
| `SchedulingWorkerProposeNewTimeIntentPropertyTests.cs` | 3 CsCheck properties x 1000 samples (duration preservation, precondition monotonicity, flags round-trip) | pass |
| `GraphHostAdapterClientProposeNewTimeTests.cs` | method/route (escaped principal + id), headers (bearer, client-request-id, no Prefer), exact two-property body + absent-property guardrail via JsonDocument, 202-empty-body -> ok:true/data:null, D5 error samples, 429 exhaustion under FakeTimeProvider | pass |
| `HostAdapterHttpClientProposeNewTimeTests.cs` | local-backend `NOT_SUPPORTED`, zero HTTP I/O, zero token reads, null-requestId self-generation | pass |
| `HostAdapterSchedulingServiceProposeNewTimeTests.cs` | delegation, correlation-id forwarding, failure-envelope throw message, id guard | pass |
| Full project (`OpenClaw.Core.Tests`, this review's plain run) | 930 | 930/930 pass |
| Full solution (executor final settings run) | 1377 | 1377 pass, 5 pre-existing COM/publish skips |

## Appendix B: Command Reference

```
git diff --name-status 273c7df25d3a5a0cd928a47e8c80fced592ce06b..HEAD
git diff --stat 273c7df25d3a5a0cd928a47e8c80fced592ce06b..HEAD
csharpier --version   (1.3.0)
csharpier check src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs src/OpenClaw.Core/CloudGraph/GraphHostAdapterClient.ProposeNewTime.cs src/OpenClaw.Core/HostAdapterHttpClient.cs src/OpenClaw.Core/Agent/Runtime/HostAdapterSchedulingService.cs src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs
dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --collect:"XPlat Code Coverage" --results-directory "docs/features/active/2026-07-07-attendee-propose-new-time-130/evidence/qa-gates/coverage-review/plain-mode"
python parse_cov.py   (scratch per-file cobertura parser, settings + plain)
git show -s --format=%cI 273c7df25d3a5a0cd928a47e8c80fced592ce06b   (baseline-freshness check)
grep -rn "CloudGraph|Microsoft.Office|ComVisible" src/OpenClaw.Core/Agent/
grep -rn "RecordMoveAsync|series_moves|ISeriesMoveHistory" src/OpenClaw.Core/Agent/Runtime/SchedulingWorker.ProposeNewTime.cs
git diff --name-only 273c7df..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'
git diff --name-only 273c7df..HEAD | grep -E '^(\.github/workflows/|scripts/benchmarks/|\.github/actions/)'
```

## Summary

Zero blocking findings. All uniform T1-T4 gates (format, lint, nullable, architecture, line/branch
coverage, changed-line no-regression) hold, verified by fresh independent runs; the executor's
committed settings-mode figures match this review's parse, and this review's plain-mode run
additionally attests the async orchestration body that settings-mode instrumentation excludes. The
fail-closed attendee calendar-write gate, the no-move-guard/no-`series_moves` invariant, the
additive-only cross-module contract change, the property-test-density gate, and the AC-9
human-interaction exception record all pass. See `code-review.2026-07-07T06-35.md` for one Minor
and two Info non-blocking observations.
