---
name: per-file-coverage-masking
description: Per-project coverage aggregates can pass uniform thresholds while a new file fails; always re-measure per-new-file coverage from cobertura
metadata:
  type: feedback
---

When auditing C# coverage, the executor's committed evidence (coverage-delta.md, final-test.md)
reports per-PROJECT line/branch rates (e.g. OpenClaw.MailBridge 90.90%/80.39%). A passing project
aggregate can hide a NEW file that is below the uniform new-code thresholds (line >= 85%,
branch >= 75% per quality-tiers.md).

**Why:** On issue #73, ComMessageSource.cs (new) was 80.1% line / 60.9% branch — a FAIL — while the
MailBridge project aggregate passed. The masking happened because the new file's untested COM-only
SMTP-resolution chain was a small fraction of the project's total lines.

**How to apply:** Do not trust per-project aggregates for the new-code verdict. Re-run
`dotnet test ... --collect:"XPlat Code Coverage" --results-directory "<FEATURE>/evidence/qa-gates/coverage-review"`
(the `--results-directory` flag routes cobertura output straight to the canonical path — worked on
the #19 review, 2026-07-02), then parse the cobertura and aggregate hits per `filename` for each
new/changed file individually. Cobertura may contain duplicate `<class>` entries per partial-class
file and the results dir may hold stale runs from an interrupted attempt — dedupe/delete before
pooling (on #19 an interrupted attempt left 3 byte-identical extra reports that doubled raw counts). The on-disk artifacts under
`artifacts/coverage/*` are gitignored and frequently stale/missing the new classes, so generate a
fresh run. See [[artifact-validator-quirks]].

Also: spec.md AC format varies by feature. Some specs use bold prose (`- **AC-01:** ...`, no box
to toggle — record status in the audit only); others (e.g. issue #80, full-bug mode) use real
`- [x]` checkboxes that the executor checks off during execution. Inspect the actual format before
assuming; never reformat the source either way.

Executor coverage copies under `artifacts/csharp/<baseline|post>-<ts>/` CAN be fresh and exact
(issue #80: reviewer re-run matched executor pooled numbers to the hundredth). Still re-run and
re-measure per-file — the match itself is the verification.

Third masking mode — INSTRUMENTATION SCOPE (issue #99, 2026-07-02): `mailbridge.runsettings`
sets `ExcludeByAttribute=...CompilerGeneratedAttribute...`, so ASYNC method bodies (compiled to
compiler-generated state machines) contribute zero instrumented lines. A changed file can show
100% line coverage while its entire new async body is unmeasured (HostAdapterSchedulingService
dropped 9 -> 4 instrumented lines when a throwing sync method became async). When a diff adds or
converts async methods, per-line cobertura cannot attest the changed lines — verify behaviorally
(fail-before/pass-after + branch-by-branch tests of the new body) and state the exclusion
explicitly in the audit (accepted disposition on the #99 review; pre-existing setting, not a
finding against the branch, but recommend the runsettings follow-up as Info).

Async masking recurred on #103 (2026-07-02): SchedulingWorker.Pipeline.cs showed identical
instrumented figures (20/20 line, 2/4 branch) before and after a +63-line async fallback was
added — the entire new body was invisible to cobertura. Same accepted disposition as #99:
behavioral verification (fail-before EXIT-1 evidence + branch-both-ways tests incl. opt-out
DataRows) with the exclusion stated explicitly in the audit. Useful technique: map the
partial-condition line numbers to source at BOTH baseline and head to prove the residual
branch misses are the same pre-existing members merely shifted by the added lines.

Extreme form on #105 (2026-07-02): an ENTIRE new production file (`CoreCacheRepository.SeriesMoves.cs`,
all members async) reported zero instrumented lines — "NOT INSTRUMENTED" in cobertura, not a low
percentage. Same accepted disposition (behavioral verification: 12 dedicated tests covering both
public methods and every branch, exclusion stated explicitly in the audit; executor had disclosed
it openly in coverage-comparison.md). For all-new async files there is no fail-before run — the
behavioral evidence IS the dedicated test suite; check every branch arm has a test (blank-key
DataRows, ON CONFLICT via direct row count, both lazy-ensure flag states, upgrade, restart).

Recurred on #107 (2026-07-02): all-async persistence partial `CoreCacheRepository.AuditLog.cs`
(185 lines, only 13 sync guard/helper lines instrumented) plus the async `WriteAuditSafelyAsync`
D4 helper. Same accepted disposition: behavioral verification (22 directed cases + CsCheck
round-trip property for the store; red/green expect-fail pair EXIT 1 -> EXIT 0 for the four
worker emission points and both resilience catch paths), exclusion stated explicitly. The #103
baseline-line-mapping technique (prove partial branches are pre-existing lines merely shifted:
same condition-coverage 1/2 at baseline line numbers, confirmed via `git show <base>:<file>`)
worked again for two unchanged ternaries.

Auto-property variant on #109 (2026-07-02): a MODIFIED production file consisting solely of
auto-properties (`AgentPolicyOptions.cs`) is entirely absent from cobertura at both baseline and
head — auto-property accessors are compiler-generated and fall under the same runsettings
`ExcludeByAttribute=CompilerGeneratedAttribute` filter. No changed-line denominator exists, so no
regression is possible; accepted disposition is the same as the async cases: state the exclusion
explicitly and verify behaviorally (defaults/binding/truth-table/property tests reading and
writing the added properties directly).

PowerShell variant (#111, 2026-07-02): Pester v5 emits COMMAND coverage only — no branch
percentage exists for PowerShell at all. Accepted disposition (precedent #58/#62, re-accepted
on the #111 review): grade the 75% branch gate on the command-coverage proxy, because commands
inside untaken branch arms register as uncovered commands. Corollary: every missed command IS
an untaken arm — locate it via the JaCoCo per-line `nr`/`mi` attrs in
`artifacts/pester/powershell-coverage.xml` and NAME the scenario (on #111 the single missed
command was the wrapper's `RecipientAdministrativeUnitScope` pass-through arm, tested only
against mocks — graded Minor with a concrete one-case seam-test recommendation).

Masking also happens at the BRANCH level, not just line level: on issue #18 (2026-07-02) the
executor's coverage-comparison reported per-file LINE only; the new OutlookScanner.Redaction.cs
was 100% line but 71.43% branch (10/14) — a Blocking FAIL against the 75% new-file gate — hidden
behind a passing package branch aggregate (87.31%). Always compute per-file line AND branch
(condition-coverage attrs) for every new/changed file. Root cause there: all sensitive-message
tests used non-meeting items, leaving ternary true-arms uncovered — uncovered branches usually
point at a real untested scenario, name it in the finding.
