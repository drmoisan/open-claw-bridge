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
