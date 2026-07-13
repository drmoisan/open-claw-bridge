# AC1-AC14 Summary and Evidence Cross-Reference

Timestamp: 2026-07-12T11-45

| AC | Status | Supporting evidence |
|---|---|---|
| AC1 (cross-service mismatch abort) | PASS | P2-T4 (guard function), P2-T5 (wiring), P2-T7 (test pass) |
| AC2 (same-wrong-version abort) | PASS | P2-T4, P2-T5, P2-T7 |
| AC3 (matched proceeds to load+up) | PASS | P2-T5, P2-T7 |
| AC4 (`-SkipDocker` bypasses guard) | PASS | P2-T5, P2-T7 |
| AC5 (module functions added/exported) | PASS | P1-T1, P1-T2, P1-T3, P1-T4 |
| AC6 (edge-case distinguishing failures) | PASS | P1-T8, P1-T9, P1-T10 |
| AC7 (module unit tests exist/pass) | PASS | P1-T6, P1-T7, P1-T8, P1-T9, P1-T10 (12/12 passing) |
| AC8 (guard tests exist/pass) | PASS | P2-T1, P2-T7 (5/5 new guard tests passing) |
| AC9 (#142 invariants green) | PASS | P3-T2 (`git diff` empty on 4 named files) |
| AC10 (#144 invariants green) | PASS | P3-T3 (5 grep checks + 2 zero-diff files) |
| AC11 (no `Import-Module` in `Install.ps1`) | PASS | P3-T1 (zero grep matches) |
| AC12 (full toolchain single-pass) | PASS | P4-T1 (format), P4-T2 (analyze), P4-T3 (test+coverage) |
| AC13 (coverage thresholds, no regression) | PASS | P4-T4 (coverage-comparison: line 91.04%, instruction-proxy 88.96% aggregate, both improved vs. baseline) |
| AC14 (full regression, no new failures) | PASS | P3-T4 (424/433 passed; the 9 failures are the identical pre-existing baseline set) |

## Notable finding carried into this summary

Implementing the Stage 9 guard (P2-T3/P2-T4/P2-T5) as specified caused 19 new failures in `tests/scripts/Install.Tests.ps1` and `tests/scripts/Install.Force.Tests.ps1` when the full regression suite was first run at P3-T4, because both files' `Get-Content` mocks had no `docker-compose.yml` fixture branch. Per P3-T4's own task text ("If any test fails, apply the needed fix and restart from Phase 1 or Phase 2 as applicable, then re-run this task"), the same fixture branch already used in `Install.DockerStage.Tests.ps1` (P2-T1) was added to both files, and the Phase 2 toolchain loop was re-run for the two newly-touched files (0 format changes, 0 analyzer findings, 54/54 targeted tests passing). This extends the plan's stated "Test files (2)" scope to 4 test files (2 new/extended as originally scoped, plus 2 additional pre-existing test files whose fixtures required a mechanical, test-only update to remain accurate against the new guard behavior). No production logic in `Install.Tests.ps1`/`Install.Force.Tests.ps1` was touched — only the shared `$script:GetContentMock` fixture gained the same `*docker-compose.yml` branch pattern already established in the plan's own P2-T1 task. This is flagged for visibility in the final completion report.

All 14 acceptance criteria checkboxes in `spec.md`'s `## Acceptance Criteria` section have been changed from `- [ ]` to `- [x]` with criterion text unchanged.
