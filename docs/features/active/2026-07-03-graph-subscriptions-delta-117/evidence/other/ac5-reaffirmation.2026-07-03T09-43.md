# AC-5 Re-affirmation (Remediation Cycle 1, Issue #117)

Timestamp: 2026-07-03T09-43

## Determination

AC-5 ("Full C# toolchain passes ...; line coverage >= 85% and branch coverage >= 75% hold with changed lines covered; ... no file exceeds 500 lines") was graded PARTIAL by `feature-audit.2026-07-03T02-34.md` solely because of finding B-117-01 (two new files below the 75% per-file branch gate). With this cycle's remediation complete, the existing `[x]` check-off is re-affirmed as accurate as of 2026-07-03T09-43.

## Dated citations

- Coverage verification (P5-T4, 2026-07-03T09-40): `evidence/qa-gates/coverage-verification.2026-07-03T09-40.md` — GraphSubscriptionManager.cs 100.00% branch, CoreCacheRepository.Subscriptions.cs 100.00% branch (both previously 50.00%), GraphDeltaReconciler.cs 93.75% (previously 75.00% zero-margin); pooled 92.84% line / 83.63% branch, no regression versus the 92.83%/83.25% baseline and reviewer reference.
- Final clean-pass toolchain (P5-T1..P5-T3, 2026-07-03T09-28..09-40): `evidence/qa-gates/csharpier.2026-07-03T09-28.md` (EXIT 0), `evidence/qa-gates/dotnet-build.2026-07-03T09-28.md` (EXIT 0, 0 warnings / 0 errors), `evidence/qa-gates/dotnet-test-coverage.2026-07-03T09-40.md` (EXIT 0, 1163 passed / 0 failed / 5 pre-existing skips, single clean pass).
- File-size and scope (P5-T5, 2026-07-03T09-42): `evidence/qa-gates/diff-scope-and-file-size.2026-07-03T09-42.md` — every modified .cs file <= 407 lines.

## Checkbox-state verification (no edits performed)

- `<FEATURE>/spec.md` line 145: `[x]` — verified accurate, unchanged.
- `<FEATURE>/user-story.md` line 60: `[x]` — verified accurate, unchanged.
- `<FEATURE>/issue.md` line 31 (mirror): `[x]` — verified accurate, unchanged.
- `git diff --stat` over the three files: empty — no AC source file text or checkbox state was modified by this cycle (per the remediation-inputs directive, the check-off is re-confirmed, not re-edited).

## AC Status Summary

### Acceptance Criteria Status
- Source: docs/features/active/2026-07-03-graph-subscriptions-delta-117/spec.md and docs/features/active/2026-07-03-graph-subscriptions-delta-117/user-story.md (issue.md mirrors the same list)
- Total AC items: 5
- Checked off (delivered): 5
- Remaining (unchecked): 0
- Items remaining: none
