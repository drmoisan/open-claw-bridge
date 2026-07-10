---
name: review-env-fallbacks
description: Fallbacks when MCP template/validator tools and dotnet tool restore are unavailable during feature review in this repo
metadata:
  type: project
---

Two environment gaps recur when running feature review in this repo's worktrees:

1. **MCP tools absent from the agent toolset.** `resolve_policy_audit_template_asset` and
   `validate_orchestration_artifacts` may not be exposed. No repo-local template files exist
   (glob for `*template*.md` finds only `.github/prompts/execute-plan-template.md`).
   **How to apply:** mirror the structure of the most recent accepted artifact set — the #99 set
   `docs/features/active/2026-07-02-wire-sendmail-runtime-99/*.2026-07-02T11-25.md` (earlier
   accepted sets: #19 `calendar-overlap-filter-19/*.2026-07-02T08-24.md`, #80
   `core-response-status-roundtrip-80/*.2026-07-02T07-35.md`; check archive locations after
   merge) — combined with the
   exact rules in [[artifact-validator-quirks]]. Record the missing MCP resolution as a documented
   exception in section 8 of the policy audit (accepted on #80, #19, #18, #99, and #101 reviews,
   2026-07-02).

2. **`dotnet tool restore` fails** ("command csharpier ... package contains dotnet-csharpier").
   **How to apply:** use the globally installed `csharpier check .` (1.3.0) instead; record the
   accommodation in the audit. Same workaround was accepted in the #70 and #80 audits.

**Why:** both gaps otherwise stall the review; prior audits established the accepted fallbacks.

Also: no `validate_evidence_locations.py` exists in this repo — do the evidence-location scan with
`git diff --name-only <base>..HEAD | grep -E '^artifacts/(baselines|baseline|qa|qa-gates|evidence|coverage|regression-testing|post-change)/'`.

Third recurring quirk — **PR-context summary misclassifies C# branches as docs-only.** On the
#99, #101, #103, #105, #107, and #109 reviews (2026-07-02) `artifacts/pr_context.summary.txt`
"Changed files overview" reported "Core logic changes: 0 files" while the authoritative git diff
contained 2-11 production and 2-7 test `.cs` files. Never scope from the summary's file
categorization; always use `git diff --stat <merge-base>..HEAD` and record the mismatch as an
observation under Rejected Scope Narrowing (accepted pattern on all six reviews). The summary's
author-asserted autoclose list can also contain non-issue tokens parsed from AC labels or spec
prose (`#AC-1`, `#ISO-8601` on #105; `#74`/`#75`/`#ISO-8601` on #107; `#107`/`#AC-1`..`#AC-5` on
#109 — real issue numbers cited as context in spec text, not closed by the branch) — noise, note
and ignore. Newest artifact-set templates after #99: #103
`2026-07-02-ordinary-mail-candidates-103/*.2026-07-02T13-36.md`, #105
`2026-07-02-one-on-one-move-history-105/*.2026-07-02T14-35.md`, #107
`2026-07-02-outbound-audit-log-107/*.2026-07-02T15-50.md`, #109
`2026-07-02-calendar-write-flags-109/*.2026-07-02T16-42.md`, and #111 (first PowerShell-only
branch) `2026-07-02-exchange-rbac-scripts-111/*.2026-07-02T18-20.md` (structural self-check
passed; validator tool unavailable). The C#-branch misclassification did NOT occur on the
PowerShell-only #111 branch — the summary categorized it correctly.

#113 (2026-07-02, C# CloudAuth module): misclassification recurred (7th C# branch); autoclose
noise recurred (`#109`/`#74` design-precedent citations + `#AC-*`/`#ISO-8601` tokens). #115
(2026-07-02, CloudGraph adapter, 12 prod + 19 test .cs): 8th recurrence of both quirks
(`#113`/`#74` precedent citations + `#AC-*`/`#ISO-8601`/`#OR-5` tokens). #117 (2026-07-03,
CloudSync, 20 prod + 21 test .cs): 9th recurrence of both quirks (`#113`/`#74` precedent
citations + `#AC-2`/`#HI-1`/`#ISO-8601` tokens). Newest validator-shaped C# artifact-set
template: #115 `2026-07-02-graph-backed-adapter-115/*.2026-07-02T21-08.md` (structural
self-check passed; prior: #113 `2026-07-02-app-only-auth-module-113/*.2026-07-02T19-27.md`).
The #117 set `2026-07-03-graph-subscriptions-delta-117/*.2026-07-03T02-34.md` is the first
FAIL-verdict (Blocking + remediation-inputs) artifact set shaped on that template.

#117 re-audit R4 (2026-07-06, epic-branch era): `mcp__drm-copilot__collect_pr_context` was ALSO
absent from the toolset and the on-disk pr_context pair was stale (pre-rebase head vs origin/main).
Accepted fallback: regenerate both files manually from git at the canonical `artifacts/pr_context.*`
paths (base/head/merge-base, commits, name-status categorized by src/tests/docs), state the
accommodation in policy-audit Section 8 Approved Exceptions. Features now merge into EPIC
integration branches (e.g. `epic/openclaw-vision-integration`), not main — resolve merge-base
against the supplied epic base. Rebase-equivalence technique: `git diff <pre-rebase-head>
<rebased-commit>` returning exactly the base delta proves feature content is byte-identical and
the old baseline coverage stays valid (base delta had zero .cs). Parser gotcha: XPlat cobertura
writes `branch="True"` (capital T) — compare case-insensitively or branch totals silently read 0.
The #117 R4 set `2026-07-03-graph-subscriptions-delta-117/*.2026-07-06T22-26.md` passed the
structural self-check.

#120 (2026-07-06): PR-context artifacts were entirely ABSENT (not just stale) and no repo-local
collector script exists — accepted fallback: generate `artifacts/pr_context.summary.txt`
(branch/head/base/merge-base+timestamp, commit list, name-status, diff stat) and
`pr_context.appendix.txt` (full `git diff base..HEAD`) directly from git, and record the
accommodation under Rejected Scope Narrowing + Section 8. Self-generated summaries do not carry
the docs-only misclassification quirk. Newest validator-shaped C# artifact template: #120
`2026-07-06-negative-scope-smoke-test-120/*.2026-07-06T23-55.md` (PASS-verdict, dual-mode
coverage sections; structural self-check passed).

#119 (2026-07-06, F15 allowlist, epic-branch base): PR-context artifacts were ABSENT at review
start even though the caller prompt claimed they were "refreshed" — never trust the caller's
freshness claim; `ls artifacts/pr_context.*` first, and regenerate with raw git (log/name-status/
stat -> summary; full diff -> appendix) when missing. No collector script exists in scripts/.
Epic-child reviews resolve base to the epic integration branch (e.g. `epic/openclaw-vision-integration`),
not main. Newest validator-shaped artifact-set template: #119
`2026-07-06-send-on-behalf-allowlist-119/*.2026-07-06T23-41.md` (structural self-check passed).

Fifth quirk — **executor baseline can predate the merge-base**: on #119 the committed baseline
(22:45) was captured 4 minutes BEFORE the merge-base commit (22:49, a sibling-feature merge into
the epic branch), so baseline test counts/coverage omitted that merge. Detection: compare baseline
timestamp vs `git show -s --format=%cI <merge-base>`, and grep the baseline cobertura for classes
the base merge added. Grade Minor (non-gating) IF `git diff <mb>^1..<mb> -- <touched paths>` is
empty (per-file baselines then exact) and head measurements are fresh; otherwise it can mask a
real regression.

#124 (2026-07-07, T1 OpenClaw.Core, CloudSync audit instrumentation): PR-context artifacts were
absent (no `artifacts/pr_context.*` files); reviewed directly off `git diff
origin/epic/openclaw-vision-integration...HEAD` (merge-base `7a29286`) per the epic-child
base-resolution rule from #119. Newest validator-shaped C# artifact template:
`2026-07-07-graph-activity-log-purview-124/*.2026-07-07T06-54.md`. This review is also the first
to independently re-run `dotnet test --collect:"XPlat Code Coverage"` AND parse the resulting
Cobertura report with a scratch script rather than trusting the executor's committed coverage
evidence numbers outright — the independent figures matched the committed evidence exactly
(OpenClaw.Core 93.03% line / 81.45% branch), which is itself useful confirmation the executor's
reporting practice on this feature was accurate.

#130 (2026-07-07, F19 attendee-propose-new-time, the F18 SIBLING/mirror; final openclaw-vision
feature, Epic D): PR-context artifacts were PRESENT this time (caller-provided in the worktree),
but the summary "Changed files overview" STILL misclassified the C# branch as docs-only
(`Core logic changes: 0 files`, 20 docs; real diff 9 prod + 6 test .cs) — 10th recurrence; scope
from `git diff --stat <mb>..HEAD`, not the summary. Autoclose noise recurred (`#AC-1..#AC-9`,
`#HI-1`, `#ISO-8601` + `#107/#109/#119/#128` precedent citations; only `#130` closes). Verdict
PASS, zero blocking (1 Minor + 2 Info). Structural self-check passed; MCP validator/template tools
unavailable (mirrored the #128 F18 set). Newest validator-shaped PASS C# template:
`2026-07-07-attendee-propose-new-time-130/*.2026-07-07T06-35.md`. Notable: F19 modified ZERO
pre-existing test files (loose Moq mocks absorbed the new IHostAdapterClient/ISchedulingService
members automatically), contrast F18 which mechanically edited 4 worker test files.

#128 (2026-07-07, F18 organizer reschedule, first calendar-write RPC): PR-context artifacts
absent again (5th occurrence) — regenerated from git per the #120 recipe. Baseline freshness
check (`git show -s --format=%cI <mb>` vs artifact mtime) passed this time (baseline 04:03 >
merge-base 03:10). `--results-directory "<FEATURE>/evidence/qa-gates/coverage-review/{settings,plain}-mode"`
cleanly routes BOTH dual-mode runs to canonical paths. Manual verification of the
orchestrator-state `human_interaction` invariants (no Python validator in this repo) accepted for
an AC that requires an `exception` + `runbook_path` record. Newest validator-shaped C# artifact
template: `2026-07-07-organizer-reschedule-128/*.2026-07-07T04-50.md` (PASS-verdict, dual-mode
coverage, prior: #124).

Fourth recurring quirk — **`run_poshqc_test` MCP tool fails in this repo** (pre-existing
workspace defect, first hit on #111 execution, accepted on the #111 review): the bundled
`pester.runsettings.psd1` hardcodes drm-copilot `CodeCoverage.Path` entries, six of which do
not exist here, so Pester 5.6.1 coverage fails at RunStart and the wrapper exits 4294967295
even though tests pass. Accepted fallback (precedent features #58/#62): run the identical
bundled `Invoke-PoshQCTest` pipeline directly with repo-scoped coverage settings; raw outputs
land at `artifacts/pester/powershell-coverage.xml` (JaCoCo format — parse per-file LINE
counters and per-line `nr`/`mi` attrs to find missed commands). `run_poshqc_format` and
`run_poshqc_analyze` work fine. Reviewer-side independent signal when MCP tools are absent
from the toolset: PSScriptAnalyzer 1.24.0 + `Invoke-Formatter -ScriptDefinition` idempotency
check with defaults, plus a plain `Invoke-Pester` re-run (all clean on #111).

#135 (2026-07-07, PowerShell-only, `$OFS`-collapse `.env` corruption bug fix, minor-audit):
no MCP tools at all were available to this reviewer (not `run_poshqc_*`, not
`resolve_policy_audit_template_asset`, not `validate_orchestration_artifacts`, not
`collect_pr_context`) — a step up from prior "some tools missing" cases. PR-context artifacts
were present and already fresh (head SHA matched exactly); no regen needed. Full independent
verification was still possible with only `pwsh`/`PSScriptAnalyzer`/`Pester` installed locally:
`Invoke-Formatter` idempotency (0 diffs), `Invoke-ScriptAnalyzer` default rules (0 findings),
and a real `Invoke-Pester` re-run of the targeted scope reproduced the executor's exact pass
count (54/54). Confirmed the executor's committed `artifacts/pester/powershell-coverage.xml`
(JaCoCo) directly: root `<counter type="INSTRUCTION">` covered/missed reproduces the reported
repo-wide command-coverage % arithmetically, and `<line nr="N" mi="0" ci="1">` on each changed
line proves no regression — do this instead of trusting the executor's coverage-comparison
prose. Gotcha reproduced firsthand: `tests/scripts/Publish.Tests.ps1` only resolves
`Invoke-VersionStamp` (from `Publish.Msix.psm1`) if `Publish.Msix.Tests.ps1` discovers first in
`Pester.Run.Path`; an arbitrary/alphabetical-by-intuition file array order (not sorted) gave 25
false failures until the array was reordered to put the Msix file first — Pester does NOT sort
`Run.Path` when it's an explicit array. First review where the caller's delegation prompt
contained a positive, explicit statement rejecting scope narrowing ("Determine review scope
yourself... per your scope invariant") rather than attempting narrowing — `## Rejected Scope
Narrowing` correctly recorded "none" rather than being left out. Newest validator-shaped
PowerShell-only artifact template (all-PASS, zero MCP tools, three-file audit set):
`2026-07-07-env-array-wrap-corruption-135/*.2026-07-07T16-05.md`.

#135 re-audit cycle 2 (2026-07-09): same branch, second work cycle after PR #136 opened
(AC-7..AC-10, `[AllowEmptyString()]` fix for a `Write-EnvFileContent` parameter-binding defect
on blank-line `.env` fixtures). Still zero MCP tools available. Caller prompt this time
explicitly instructed the OPPOSITE of narrowing ("review the full branch diff... do not narrow
scope to only the new commit's files") — comply by diffing the full merge-base..HEAD range, not
just the newest commit. Fail-before/pass-after was independently reproduced by importing
`git show <pre-fix-sha>:<module>.psm1` directly (no need to `git stash`/checkout the working
tree) and invoking the isolated repro against that in-memory module object; the executor's own
stash-based per-file coverage isolation technique (`git stash push -- <file>`, rerun coverage,
`git stash pop`) is also independently checkable by parsing the resulting JaCoCo XML for the
per-class `<counter>` blocks and cross-checking the exact covered/missed figures the executor
reported. A declarative parameter-validation attribute (e.g. `[AllowEmptyString()]`) is NOT
instrumented as an executable `<line>` by Pester's command-coverage plugin — confirmed by
finding zero `<line>` entries in the attribute's line range — so identical pre/post coverage
percentages after adding one are expected, not a red flag. New pattern: a docs-only commit
landing between two review cycles (`b7bb0cd`, bundling an unrelated README addition + agent-
memory files) had been an *uncommitted working-tree* item at cycle-1 review time (correctly
recorded there as out-of-scope) but became part of the committed branch diff by cycle-2 review
time — re-audits must re-check every "out of scope, uncommitted" observation from a prior cycle,
since it may have been committed since. Newest validator-shaped PowerShell-only re-audit
template (all-PASS, zero MCP tools, ten-AC two-cycle audit set):
`2026-07-07-env-array-wrap-corruption-135/*.2026-07-09T20-15.md`.
