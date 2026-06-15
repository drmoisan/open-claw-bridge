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
`dotnet test ... --collect:"XPlat Code Coverage"` to the canonical
`<FEATURE>/evidence/qa-gates/coverage-review/` path, then parse the cobertura and aggregate hits
per `filename` for each new/changed file individually. The on-disk artifacts under
`artifacts/coverage/*` are gitignored and frequently stale/missing the new classes, so generate a
fresh run. See [[artifact-validator-quirks]].

Also: spec.md ACs in this repo are authored as bold prose (`- **AC-01:** ...`), NOT markdown
checkboxes, so feature-audit AC check-off has no box to toggle — record status in the audit only and
do not reformat the source.
