# Acceptance Criteria Summary (Issue #142, P5-T7)

Timestamp: 2026-07-10T19-10
AC source (full-bug mode): docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/spec.md, `## Acceptance Criteria`.

| AC | Status | Evidence |
|---|---|---|
| AC1  Image tar produced (build x2 + single save, 4 refs) | PASS | Publish.Docker.Tests.ps1 save-vector + facade-ordering Its; Publish.ps1 Stage 3b; Publish.Tests.ps1 stage-order test (phase1/phase3 loops) |
| AC2  Build args mirror compose | PASS | Publish.Docker.Tests.ps1 exact core/agent build-vector Its; cross-reference comment in Publish.Docker.psm1 header |
| AC3  Bundle compose transformed (no build:, pull_policy: never, versioned image:, others preserved) | PASS | Publish.Docker.Tests.ps1 positive + real-file drift-guard Its |
| AC4  Transform fails fast on drift | PASS | Publish.Docker.Tests.ps1 two negative-path throw Its |
| AC5  Dev compose path unaffected (no diff to tracked docker-compose.yml) | PASS | invariant-diffs (empty diff) |
| AC6  Manifest correct at any size ([long]) | PASS | ps-expect-fail-manifest-size (fail-before); Publish.Helpers.Tests.ps1 [long] >2GiB test passes after fix; Stage 3b runs before Write-PublishManifest |
| AC7  Install loads before compose up; skipped under -SkipDocker | PASS | ps-expect-fail-install-load (fail-before); Install.DockerStage.Tests.ps1 image-load-stage + -SkipDocker Its; Install.Tests.ps1 ordering test |
| AC8  Load failure modes explicit (missing tar, non-zero exit) | PASS | Install.Docker.Tests.ps1 missing-tar and non-zero-exit throw Its |
| AC9  Wrapper seam in place, each module <500, four Install.Helpers sites unmodified | PASS | line-count checks (Publish.Docker 373, Install.Docker 103); seam-hermeticity-checks; invariant-diffs (Install.Helpers.psm1 unchanged) |
| AC10 Bundle staging complete (Install.Docker.psm1 staged; dev compose not copied) | PASS | Publish.Helpers.Tests.ps1 5-file staging-order test + dev-compose-removal test |
| AC11 Tests hermetic (mock only the seam, no shim, no temp files) | PASS | seam-hermeticity-checks (zero matches) |
| AC12 No regression, clean toolchain single pass | PASS | final-poshqc-format/analyze (clean); final-poshqc-test (406 passed, 0 failed); coverage-comparison (89.91%, no regression, all changed files above thresholds) |

All 12 checked off in spec.md (`- [ ]` -> `- [x]`, criterion text unchanged).

### Acceptance Criteria Status
- Source: docs/features/active/2026-07-10-installer-docker-images-not-bundled-142/spec.md
- Total AC items: 12
- Checked off (delivered): 12
- Remaining (unchecked): 0
- Items remaining: none
