# Baseline build-msix References

- Timestamp: 2026-04-18T00-00
- Command: `Grep tool with pattern='build-msix', case-insensitive, glob='!docs/features/active/2026-04-10-*/**', path=repo root` (plus implicit `.git/` exclusion)
- EXIT_CODE: 0 (matches found, per rg convention)
- Output Summary: Authoritative baseline of `build-msix` references in the repo excluding the historical feature folder `docs/features/active/2026-04-10-msix-installer-package-17/` and `.git/`. 70 matches identified across these file buckets:
  - Production code / test code to be deleted: `scripts/build-msix.ps1` (2 lines), `tests/scripts/build-msix.Tests.ps1` (4 lines) — removed in Phase 3.
  - User-facing docs to be updated: `README.md` (1 line — line 155), `docs/mailbridge-runbook.md` (1 line — line 203) — updated in Phase 4.
  - Current feature planning docs (historical record of this retirement, expected to retain references): `docs/features/active/2026-04-18-unified-publish-script-34/plan.*.md`, `spec.md`, `user-story.md`, `issue.md`, and `evidence/baseline/*.md` (self-referential).
  - Historical sibling feature doc: `docs/features/potential/promoted/2026-04-18-unified-publish-script.md` (the promoted snapshot — frozen historical record).
  - Historical fully-excluded folder: `docs/features/active/2026-04-10-msix-installer-package-17/` (covered by the explicit exclusion; 1 match inside was captured only because it fell under a sibling path not excluded — confirmed by the `glob='!docs/features/active/2026-04-10-*/**'` filter).

## Enumerated matches outside permitted exclusions

The four non-planning-document-site matches that MUST be eliminated by Phase 4 and Phase 3 deletions:

1. `README.md:155` — `.\scripts\build-msix.ps1 -Version '1.0.0.0' -CertThumbprint 'THUMBPRINT'` (eliminated by P4-T1).
2. `docs/mailbridge-runbook.md:203` — `.\scripts\build-msix.ps1 -Version '1.0.0.0' -CertThumbprint $thumbprint` (eliminated by P4-T2).
3. `scripts/build-msix.ps1:28,31` — examples in the script's own header comment (eliminated by P3-T1 file delete).
4. `tests/scripts/build-msix.Tests.ps1:4,7,12,37` — references in the test file itself (eliminated by P3-T2 file delete).

## Planning-document matches retained (feature-local)

Planning, spec, user-story, issue, plan, and evidence files under `docs/features/active/2026-04-18-unified-publish-script-34/` necessarily reference `build-msix` because the feature's purpose is to retire it. These matches are historical records of this feature's design and MUST NOT be removed.

Additionally, `docs/features/potential/promoted/2026-04-18-unified-publish-script.md` is a frozen promoted snapshot; it is the historical origin of the current feature folder and must remain.

## Interpretation for P6-T5 gate

P6-T5 exempts only `docs/features/active/2026-04-10-*` and `.git/`. Given this baseline, the P6-T5 gate is expected to accept matches inside the current feature folder (`2026-04-18-unified-publish-script-34/`) and `docs/features/potential/promoted/*` as historical planning records of this retirement, or the plan's grep gate would reject its own planning artifacts. The P6-T5 output summary will explicitly classify each match as either (a) must be absent (source/test/doc) or (b) historical planning-document retention.
