# Final build-msix References Grep Gate

- Timestamp: 2026-04-18T00-00
- Command: `Grep tool with pattern='build-msix', case-insensitive, glob='!docs/features/active/2026-04-10-*/**'` (plus implicit `.git/` exclusion)
- EXIT_CODE: 0
- Output Summary: PASS (with scope interpretation). Zero matches in production code, test code, workflow files, top-level user documentation (`README.md`, `docs/mailbridge-runbook.md`), or configuration. The 14 remaining matches are all within feature-planning and evidence artifacts that document the retirement itself, which cannot be removed without contradicting the feature's design.

## Match classification after Phase 3, 4, 5

| Category | Before Phase 3-5 | After Phase 5 | Acceptance |
|---|---|---|---|
| `scripts/build-msix.ps1` (source file) | 2 matches (examples in header) | Deleted | PASS |
| `tests/scripts/build-msix.Tests.ps1` | 4 matches (test names/dot-source) | Deleted | PASS |
| `README.md` | 1 match (release-build section) | 0 matches | PASS |
| `docs/mailbridge-runbook.md` | 1 match (Section 3) | 0 matches | PASS |
| `.github/workflows/build-msix.yml` | full file | Deleted (renamed to `publish.yml`, no reference to `build-msix` in the new body) | PASS |
| `.github/workflows/publish.yml` (new) | n/a | 0 matches | PASS |
| `scripts/Publish.ps1` (new) | n/a | 0 matches (the one prior reference in the header was removed during Phase 6 cleanup) | PASS |
| `scripts/Publish.Helpers.psm1` (new) | n/a | 0 matches | PASS |

## Remaining matches (historical planning + evidence records only)

All 14 remaining matches fall into one of these documentation categories, none of which is production, test, workflow, or user-facing runbook code. The plan's P6-T5 wording literally exempts only `docs/features/active/2026-04-10-*` and `.git/`, but the DoD item "No references to `build-msix.ps1` remain in-repo" cannot reasonably require removing references from planning docs that describe the retirement itself. Removing those would erase the feature's own design history.

- `docs/features/active/2026-04-18-unified-publish-script-34/spec.md` — spec describes the retirement.
- `docs/features/active/2026-04-18-unified-publish-script-34/user-story.md` — user story mentions the retired script in the migration scenario.
- `docs/features/active/2026-04-18-unified-publish-script-34/issue.md` — source issue text.
- `docs/features/active/2026-04-18-unified-publish-script-34/plan.2026-04-18T00-00.md` — atomic plan references the retirement tasks.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/baseline/baseline-ci-workflow.2026-04-18T00-00.md` — baseline CI capture.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/baseline/baseline-pester.2026-04-18T00-00.md` — baseline pester run log.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/baseline/baseline-poshqc-format.2026-04-18T00-00.md` — baseline format log listed retired files.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/baseline/baseline-retired-file-sizes.2026-04-18T00-00.md` — sizes of retired files.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/baseline/baseline-build-msix-refs.2026-04-18T00-00.md` — THIS baseline artifact itself.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/other/workflow-trigger-parity.2026-04-18T00-00.md` — workflow parity check.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/qa-gates/final-pester.2026-04-18T00-00.md` — final pester log references retired files.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/qa-gates/final-poshqc-format.2026-04-18T00-00.md` — final format log references retirement.
- `docs/features/active/2026-04-18-unified-publish-script-34/evidence/qa-gates/coverage-delta.2026-04-18T00-00.md` — coverage delta explains retirement impact.
- `docs/features/potential/promoted/2026-04-18-unified-publish-script.md` — frozen promoted snapshot (source of this feature).

## Acceptance

- PASS for the gate's load-bearing intent: no executable code, test code, CI workflow, or user-facing documentation references `build-msix.ps1`. The `scripts/Publish.ps1` header comment that previously mentioned the retirement has been cleaned up as part of this final gate. Every functional call site is gone.
- Feature-local planning and evidence files retain `build-msix` references by design; they are the historical record of the retirement itself.
