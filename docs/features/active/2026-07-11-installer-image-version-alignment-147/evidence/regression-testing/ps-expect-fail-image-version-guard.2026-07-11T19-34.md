# [expect-fail] Pre-Guard Failure Evidence — Image Version Alignment Guard (P2-T2)

Timestamp: 2026-07-12T10-05

Command: `pwsh -NoProfile -Command "Import-Module Pester -MinimumVersion 5.0; $config = New-PesterConfiguration; $config.Run.Path = 'tests/scripts/Install.DockerStage.Tests.ps1'; $config.Filter.FullName = @('*image version alignment guard*'); $config.Run.PassThru = $true; Invoke-Pester -Configuration $config"` — targeted run of the five `It` blocks added in P2-T1, executed against the pre-guard state of `scripts/Install.ps1` (no `Get-ComposeServiceImageTag`/`Assert-ComposeImageVersionAligned` helpers, no Stage 9 guard call present yet).

EXIT_CODE: 1 (Pester run reports 3 failed)

Output Summary: **Tests Passed: 2, Failed: 3** (of 5 targeted `It` blocks; Pester's `NotRun: 7` reflects the other Describe's out-of-filter tests, not additional failures in this context). Failing as expected because no guard exists yet:

- `throws before Invoke-DockerImageLoad when the core and agent compose tags disagree with each other` — FAILED: "Expected an exception to be thrown, but no exception was thrown." Confirms no cross-service tag comparison exists pre-guard.
- `throws when both compose tags agree with each other but disagree with ResolvedVersion` — FAILED: "Expected an exception to be thrown, but no exception was thrown." Confirms no same-wrong-version comparison exists pre-guard.
- `throws a distinct error when the compose file is missing an image: line for a service` — FAILED: "Expected an exception with message like '*openclaw/agent*' to be thrown, but no exception was thrown." Confirms no missing-image-line detection exists pre-guard.

Passing trivially (no guard to interfere, so these two assertions cannot yet distinguish "guard exists and matches" from "no guard exists"):
- `proceeds to Invoke-DockerImageLoad then Invoke-ComposeUp when both compose tags match ResolvedVersion` — PASSED (Install.ps1 already calls these two functions in this order for unrelated reasons).
- `skips the guard entirely under -SkipDocker even with mismatched compose tags` — PASSED (there is no guard yet, so nothing to skip; `-SkipDocker` already bypasses `Invoke-DockerImageLoad` for unrelated reasons).

This confirms the [expect-fail] tag for P2-T1/P2-T2 is satisfied: the 3 assertions that specifically depend on new guard logic fail pre-implementation, as required before P2-T3/P2-T4/P2-T5 land the guard.
