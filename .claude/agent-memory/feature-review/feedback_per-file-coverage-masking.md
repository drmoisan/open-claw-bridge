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

Recurred on #113 (2026-07-02): NEW file `ClientCredentialsTokenProvider.cs` reported 37/37 = 100%
line while its entire async `RefreshAsync` body (lines 78-135: semaphore, double-check, credential
call, both catches, cache write) was uninstrumented — and this time the executor's committed
coverage-comparison claimed the 100% per-file figure WITHOUT stating the exclusion (it disclosed
the analogous auto-property file openly). Detection technique: list the instrumented line NUMBERS
for the file from cobertura and diff against the source's executable ranges — a contiguous gap
matching an async method body is the tell. Same accepted disposition (behavioral verification per
arm + exclusion stated explicitly in the audit, Info grade).

Recurred on #115 (2026-07-02): BOTH core async control-flow bodies of the new Graph adapter —
`GraphRequestExecutor.ExecuteAsync` (retry loop, lines 55-157) and
`GraphHostAdapterClient.ListPagedAsync` (paging loop, lines 131-191) — uninstrumented while the
files reported 97.40%/100% line; executor's coverage-comparison again claimed the 99.71% new-code
figure WITHOUT stating the exclusion (non-disclosure pattern from #113 repeated). Same accepted
disposition (behavioral verification: 22 pipeline tests + 8 paging tests, every arm named in the
audit). Also on #115: a new file (GraphHostAdapterClient.cs) landed at EXACTLY 75.00% branch
(9/12) — meets the >= 75% gate with zero margin; the 3 partial arms were defensive null-body
throws, graded Minor with one-case test recommendations. Exact-gate files deserve a named margin
warning in the review.

New variant on #117 (2026-07-03, CloudSync): the instrumented SYNC-subset branch coverage of two
NEW files was itself below the 75% gate — GraphSubscriptionManager.cs 2/4 = 50% (ParseSubscription
null-body + missing-id throw arms untested) and CoreCacheRepository.Subscriptions.cs 1/2 = 50%
(ReadSubscription `?? MinValue` fallback untested) — while both files showed 100% instrumented line.
Distinction that decided the verdict: these arms are MEASURED-and-uncovered (no test anywhere),
not excluded-and-behaviorally-verified, so the #99-#115 async-exclusion disposition does NOT apply;
graded Blocking per the #18 precedent and remediation triggered (3-4 directed tests). The executor's
coverage-comparison DISCLOSED the async exclusion this time (improvement over #113/#115) but graded
the 50% subsets PASS via 100%-line + package-aggregate reasoning — that grading was rejected. Also:
executor per-file raw counts were exactly 2x the deduped values (duplicate class entries), ratios
identical — dedupe before comparing. GraphDeltaReconciler landed at exactly 75.00% (12/16) —
zero-margin file, named-arm Minor per the #115 pattern.

Masking also happens at the BRANCH level, not just line level: on issue #18 (2026-07-02) the
executor's coverage-comparison reported per-file LINE only; the new OutlookScanner.Redaction.cs
was 100% line but 71.43% branch (10/14) — a Blocking FAIL against the 75% new-file gate — hidden
behind a passing package branch aggregate (87.31%). Always compute per-file line AND branch
(condition-coverage attrs) for every new/changed file. Root cause there: all sensitive-message
tests used non-meeting items, leaving ternary true-arms uncovered — uncovered branches usually
point at a real untested scenario, name it in the finding.
