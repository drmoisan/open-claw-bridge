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
