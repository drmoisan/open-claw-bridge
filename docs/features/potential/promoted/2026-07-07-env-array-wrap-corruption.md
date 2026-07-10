# env-array-wrap-corruption (Potential Bug)

- Date captured: 2026-07-07
- Author: drmoisan
- Status: Promoted -> Issue #135 (docs/features/active/2026-07-07-env-array-wrap-corruption-135/)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

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

## Next Step

- [x] Promote to GitHub issue (bug-report template)
- [x] Move to active fix folder / branch
