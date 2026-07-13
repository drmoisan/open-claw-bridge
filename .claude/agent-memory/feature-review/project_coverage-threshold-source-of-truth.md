---
name: coverage-threshold-source-of-truth
description: Which coverage thresholds bind when the reviewer instructions contradict themselves (85/75 authoritative vs a stray 80/90 procedure)
metadata:
  type: project
---

The feature-review agent's own instructions contain an internal contradiction on coverage thresholds. The "Coverage Thresholds" subsection states the authoritative uniform rule (line >= 85%, branch >= 75%, no regression on changed lines) and says tier-specific lower thresholds are not used. The later "Verification Procedure" subsection still references an older scheme (repo-wide FAIL below 80%, new-file FAIL below 90%).

**Why:** The 85/75 uniform rule is what the checked-in repo policy mandates (`.claude/rules/quality-tiers.md` Authoritative Decision #2 and `.claude/rules/general-unit-test.md`). The 80/90 numbers in the Procedure are a stale leftover and conflict with both the repo rules and the reviewer's own Thresholds subsection.

**How to apply:** Apply 85/75 uniform as authoritative for PASS/FAIL coverage verdicts. When a new file lands between 85% and 90% line coverage (e.g. #148 `Set-OpenClawWebSearchProvider.ps1` at 87.50%), it PASSES; do not FAIL it on the stray 90% number. Record the discrepancy as a non-blocking caveat in policy-audit so the verdict is auditable. Repo Pester coverage has no true BRANCH counter — INSTRUCTION coverage is the accepted branch proxy; note that true branch coverage is unverified.
