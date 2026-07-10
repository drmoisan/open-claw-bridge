Timestamp: 2026-07-10T15-02

## PR Title (draft)

`fix(scripts): suppress Publish.ps1 output leak; add Deploy.ps1 wrapper entry point (#139)`

## Summary (draft)

- Fixes a defect in `scripts/Publish.ps1` where three helper call sites (`Invoke-VersionStamp`, `Invoke-MakeAppx`, `Write-PublishManifest`) did not suppress their return values, causing a captured invocation to emit multiple pipeline objects instead of exactly one (the bundle root). Fix: assign each call's return value to `$null` at the call site; helper return behavior in `Publish.Msix.psm1` / `Publish.Helpers.psm1` is unchanged.
- Adds a new `scripts/Deploy.ps1` first-class entry point that runs `Publish.ps1`, captures the returned bundle root, fails fast if publish throws or returns no bundle root, then invokes the staged `<bundleRoot>\Install.ps1`. Forwards publish parameters (`-Version`, `-Configuration`, `-CertThumbprint`, `-SkipSign`) and install parameters (`-SkipDocker`, `-DockerEnvFilePath`, `-AnthropicEnvFilePath`, `-Force`), mapping `-SkipSign` to `-AllowUnsigned`. Uses `CmdletBinding(SupportsShouldProcess = $true)` and never changes the caller's working directory.

## Files Changed

- `scripts/Publish.ps1` (modified, 3-line output-suppression fix; see `git diff --stat`: +3/-3 lines).
- `scripts/Deploy.ps1` (new, 164 lines).
- `tests/scripts/Publish.Tests.ps1` (extended, +18 lines: one new regression test in a new `Context 'output contract'`).
- `tests/scripts/Deploy.Tests.ps1` (new, 183 lines).

## Risks

- `scripts/Deploy.ps1`'s production `Invoke-PublishScript`/`Invoke-InstallScript` wrapper-function bodies (the actual `$PSCmdlet.ShouldProcess(...)`/`& ...` invocation lines) are exercised only by real (non-mocked) usage, not by the unit-test suite, which pre-registers global overrides per the wrapper-seam convention (matching the existing, accepted `Test-IsElevatedAdmin` pattern in `scripts/Install.ps1`). Per-file coverage of `Deploy.ps1` is 87.10%, above the 85% floor, but the specific guard-body lines are unit-test-blind by design.
- `Publish.ps1`'s return-contract change (single scalar instead of a leaked multi-element array) could affect a hypothetical caller that relied on the leaked array shape; per `spec.md`, this is out of scope since the leak was a defect, not a documented contract, and any caller that already discarded output (`Out-Null` or ignored) is unaffected.

## Validation Performed

- Full PowerShell toolchain (format -> analyze -> test) passes clean, repo-wide: 380 tests passed, 0 failed, 0 analyzer findings, 0 formatting changes on the final recorded pass.
- Coverage (corrected-runsettings workaround for the known MCP coverage-path defect #111/#125/#135/#137): repo-wide 89.94% (baseline 89.93%, no regression); `scripts/Publish.ps1` 97.47% line coverage; `scripts/Deploy.ps1` 87.10% line coverage. All above the 85% line / 75% branch-coverage-proxy floors required by `.claude/rules/general-unit-test.md` and `.claude/rules/powershell.md`.
- All 6 acceptance criteria verified and checked off in `issue.md`, `user-story.md`, and `spec.md`.

## Evidence Links

- Phase 0 baseline: `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/evidence/baseline/`
- Phase 1 regression evidence: `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/evidence/regression-testing/`
- Phase 2/3 QA gates: `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/evidence/qa-gates/`
- Issue update mirror: `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/evidence/issue-updates/issue-139.2026-07-10T15-02.md`

## Test Plan

- [x] `tests/scripts/Publish.Tests.ps1` — new `Context 'output contract'` regression test (fails pre-fix, passes post-fix).
- [x] `tests/scripts/Deploy.Tests.ps1` — parameter forwarding, `-SkipSign` -> `-AllowUnsigned` mapping, publish-failure/empty-bundle-root short-circuit, `-WhatIf` propagation, returned bundle root, no-CWD-change.
- [x] Full repo-wide PowerShell toolchain pass (format/analyze/test) with numeric coverage recorded.
