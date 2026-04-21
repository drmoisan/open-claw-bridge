# Baseline Line Counts (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: `wc -l` over every in-scope production and test file.
EXIT_CODE: 0
Output Summary:

## Pre-change production line counts (PB-T9)

- `scripts/Publish.ps1`: 183 (<= 500)
- `scripts/Publish.Helpers.psm1`: 456 (<= 500)
- `scripts/Install.ps1`: 210 (<= 500)
- `scripts/Install.Helpers.psm1`: 448 (<= 500)
- `scripts/Uninstall.ps1`: 88 (<= 500)

Acceptance: all production files start the refinement at <= 500 lines.

## Pre-change test line counts (PB-T10)

- `tests/scripts/Publish.Tests.ps1`: 195 (<= 500)
- `tests/scripts/Publish.Helpers.Tests.ps1`: 442 (<= 500)
- `tests/scripts/Install.Tests.ps1`: 268 (<= 500)
- `tests/scripts/Install.Helpers.Tests.ps1`: 488 (<= 500)
- `tests/scripts/Uninstall.Tests.ps1`: 163 (<= 500)

Acceptance: all test files start the refinement at <= 500 lines.

## Pre-Phase-C line counts (PC-T1)

- `scripts/Publish.Helpers.psm1`: 456 (<= 500) — confirmed pre-change.

## Post-Phase-C-Batch-1 line counts (PC-T8)

- `scripts/Publish.Helpers.psm1`: 498 (<= 500) — post `Copy-InstallScriptsIntoBundle` addition (+42 lines including formatter trimming).

## Pre-Phase-C-Batch-2 line counts (PC-T9)

- `scripts/Publish.Helpers.psm1`: 498 (<= 500) — start of batch 2; manifest-schema update will drop `generatedAt` and trim a couple of lines.
- `scripts/Publish.ps1`: 183 (<= 500) — pre staging-stage wiring.

## Post-Phase-C-Batch-2 line counts (PC-T17)

- `scripts/Publish.Helpers.psm1`: 495 (<= 500) — `generatedAt` field and help text trimmed after schema change.
- `scripts/Publish.ps1`: 189 (<= 500) — staging-stage wiring added (+6 lines).

## Pre-Phase-D line counts (PD-T1)

- `scripts/Install.Helpers.psm1`: 448 (<= 500)
- `tests/scripts/Install.Helpers.Tests.ps1`: 488 (<= 500)

## Post-Phase-D line counts (PD-T13)

- `scripts/Install.Helpers.psm1`: 464 (<= 500) — removed `Find-NewestPublishVersion` (-32 lines) and added `Get-ManifestVersion` (+40 lines including header); formatter trimmed 8 trailing-whitespace lines.

## Pre-Phase-E line counts (PE-T1)

- `scripts/Install.ps1`: 210 (<= 500)
- `tests/scripts/Install.Tests.ps1`: 268 (<= 500)

## Post-Phase-E-Batch-1 line counts (PE-T9)

- `scripts/Install.ps1`: 196 (<= 500) — removed `-Version` parameter, auto-detect block, and the `$RepoRoot`/`$PublishRoot` variables; replaced with `$BundleRoot = $SourcePath` + manifest.json presence check + `Get-ManifestVersion` call (net -14 lines).

## Post-Phase-E-Batch-2 line counts (PE-T18)

- `scripts/Install.ps1`: 196 (<= 500) — unchanged in Batch 2 (test-only batch).
- `tests/scripts/Install.Tests.ps1`: 302 (<= 500) — dropped `Find-NewestPublishVersion` mock and `-Version path` context, added `Get-ManifestVersion` mock + `$PSScriptRoot`-default tests + bundle-root self-location context (net +34 lines).
