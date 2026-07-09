# env-array-wrap-corruption (Issue #135)

- Date captured: 2026-07-07
- Author: drmoisan
- Status: Promoted -> docs/features/active/env-array-wrap-corruption/ (Issue #135)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #135
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/135
- Last Updated: 2026-07-07
- Work Mode: minor-audit

## Summary

The PowerShell `.env` update helper usage corrupts the repository-root `.env` file when run for real. Two call sites wrap the already-array-safe `Read-EnvFileContent` result in a redundant `@(...)`, which nests the returned `string[]` inside a one-element array. The nested array is stringified with `$OFS` when bound to `[string[]]` parameters, collapsing every `.env` line (including comments) into a single space-joined line and appending the target key as a duplicate second line.

## Environment

- OS/version: Windows 11, PowerShell 7+
- Python version: N/A (PowerShell defect)
- Command/flags used: `scripts/Publish.ps1` (version/env persistence path); `scripts/New-MsixDevCert.ps1` (thumbprint persistence path)
- Data source or fixture: repository-root `.env` with more than one line (at least one comment line plus two key lines)

## Steps to Reproduce

1. Start with a multi-line `.env` (for example: `OPENCLAW_PACKAGE_VERSION=1.0.2.0`, a `# comment` line, `OPENCLAW_CERT_THUMBPRINT=OLDVALUE`).
2. Run a code path that calls `@(Read-EnvFileContent -Path $envPath)` and passes the result into `Set-EnvFileValue`/`Get-EnvFileMap` and then `Write-EnvFileContent` (Publish.ps1 version persistence, or New-MsixDevCert.ps1 thumbprint persistence).
3. Inspect the resulting `.env`.

## Expected Behavior

The target key is updated in place and every other line (including comments) is preserved on its own line.

## Actual Behavior

All original lines collapse into a single space-joined blob line, and the target key is appended as a separate second line, corrupting the `.env` file.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet: Reproduced interactively. `@(Read-EnvFileContent ...)` yields a 1-element array whose only element is a nested `System.String[]`; binding that to a `[string[]]` parameter joins the inner array with `$OFS` (space). Removing the extra `@()` preserves all lines and updates only the target key.

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

## Suspected Cause / Notes

Root cause confirmed. `scripts/Publish.Env.psm1` `Read-EnvFileContent` correctly returns via the unary-comma idiom `return , ([string[]]@($lines))`, which already yields a single, pipeline-safe `string[]`. The two call sites additionally wrap that call in `@(...)`, which is redundant and harmful:

- `scripts/Publish.ps1` line 118: `$envContent = @(Read-EnvFileContent -Path $EnvFilePath)`
- `scripts/New-MsixDevCert.ps1` line 72: `$content = @(Read-EnvFileContent -Path $EnvPath)`

`scripts/Publish.Env.psm1` must NOT be changed; its comma-return idiom is the documented file-I/O seam and is correct. The `README.md` example has already been corrected by a prior session and must be left in place.

Existing Pester tests did not catch this because both `Read-EnvFileContent` mocks return a flat `[string[]]` (no unary comma), which does not match the production return shape; a single-line fixture also collapses identically with or without the extra `@()`.

## Proposed Fix / Validation Ideas

- [x] Unit coverage areas: bring the `Read-EnvFileContent` mocks into return-shape parity with production (unary-comma return) in `tests/scripts/Publish.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1`; add a multi-line `.env` regression test (comment plus two keys) that fails if the redundant `@()` is reintroduced.
- [x] Integration scenario to retest: version persistence path in `Publish.ps1`; thumbprint persistence path in `New-MsixDevCert.ps1`.
- [x] Manual verification notes: after removing the two `@()` wrappers, a 3-line fixture updates only the target key in place and preserves all lines.

## Acceptance Criteria

- [x] AC-1: `scripts/Publish.ps1` no longer wraps the `Read-EnvFileContent` call in `@()`; the assignment reads `$envContent = Read-EnvFileContent -Path $EnvFilePath`.
- [x] AC-2: `scripts/New-MsixDevCert.ps1` no longer wraps the `Read-EnvFileContent` call in `@()`; the assignment reads `$content = Read-EnvFileContent -Path $EnvPath`.
- [x] AC-3: `scripts/Publish.Env.psm1` is unchanged (its unary-comma return idiom is the correct file-I/O seam), and the `README.md` example is left as-is (not reverted).
- [x] AC-4: The `Read-EnvFileContent` mocks in `tests/scripts/Publish.Tests.ps1` and `tests/scripts/New-MsixDevCert.Tests.ps1` return via the production-parity unary-comma idiom (`return , ([string[]]@(...))`), matching the real function's return shape.
- [x] AC-5: Each of the two test files contains a multi-line `.env` regression test (at least one comment line plus at least two key lines) that asserts the persisted `.env` preserves every original line, updates only the target key in place, is not collapsed into a single space-joined line, and contains no duplicate of the target key. The test fails if the redundant `@()` is reintroduced at the call site and passes with the fix applied.
- [x] AC-6: The full PowerShell toolchain passes with no regression: PoshQC format is clean, PSScriptAnalyzer reports 0 errors, all Pester tests pass, and coverage meets repository policy (line coverage >= 85% proxy via Pester command coverage; no coverage regression on the changed files).
- [x] AC-7: scripts/Publish.Env.psm1's Write-EnvFileContent -Content parameter carries [AllowEmptyString()] in addition to its existing [AllowEmptyCollection()], matching the pattern already used by Get-EnvFileMap and Set-EnvFileValue in the same module. No other function or file is changed.
- [x] AC-8: tests/scripts/Publish.Env.Tests.ps1 contains a test asserting Write-EnvFileContent -WhatIf accepts a -Content array containing an empty-string element without throwing a parameter-binding error.
- [x] AC-9: tests/scripts/Publish.Tests.ps1 contains an end-to-end regression test covering the stage 0c path (Read-EnvFileContent -> Set-EnvFileValue -> Write-EnvFileContent) with a blank-line .env fixture, asserting no parameter-binding error occurs and the blank line is preserved verbatim in the persisted content.
- [x] AC-10: The full PowerShell toolchain passes with no regression: PoshQC format is clean, PSScriptAnalyzer reports 0 errors, all Pester tests pass with no drop from this cycle's repo-wide baseline pass count, and coverage meets repository policy with no regression on the changed lines.

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch
