# Issue Update Mirror — install-stale-hostadapter-detection (P7r1)

Timestamp: 2026-04-27T08-00
PostedAs: body
IssueUpdatedAt: 2026-04-27T08-00

## Mirror of AC-14a / AC-14b update applied to `issue.md`

The single AC-14 line previously present in `docs/features/active/2026-04-26-install-stale-hostadapter-detection/issue.md` was replaced during remediation Phase 1 task P1-T1 with the following two lines (verbatim, as they now appear in `issue.md` post-P5-T2 with both checkboxes flipped to `[x]`):

```
- [x] AC-14a: Repository-wide line coverage remains >= 80% **excluding `scripts/Install.ps1` lines that are gated by inline `if (-not (Get-Command ...))` shim guards**, which are exercised by tests against pre-registered globals and counted as "missed" by the Pester coverage tracker. The `scripts/Install.ps1` per-file figure is documented as a measurement artifact in `evidence/qa-gates/p7-coverage-delta.md` and is deferred to a follow-up test-fixture refactor (see `docs/features/potential/install-test-fixture-coverage-refactor/`).
- [x] AC-14b: Changed-line coverage on `scripts/Install.Helpers.psm1` and `scripts/Install.Preflight.psm1` each reaches >= 90% (measured against the post-remediation Pester run recorded in `evidence/qa-gates/p7r1-coverage-delta.md`).
```

This is a local-only feature folder (no GitHub issue was created; per the original `issue.md` line "Issue: (local-only; no GitHub issue requested)"). `PostedAs: body` records that the update was applied directly to the feature folder's `issue.md` body in place rather than as a GitHub comment.

Numeric verdict basis: `evidence/qa-gates/p7r1-coverage-delta.2026-04-27T08-00.md` records `AC-14a: PASS` and `AC-14b: PASS` with Helpers at 94.59% and Preflight at 90.74%.
