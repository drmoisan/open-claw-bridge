Timestamp: 2026-07-10T15-02

Exact text posted:

---

## Implementation complete

`scripts/Publish.ps1` output-leak fix and new `scripts/Deploy.ps1` wrapper entry point are implemented and verified.

### Summary
- **`scripts/Publish.ps1` fix (AC-1):** assigned the three leaking helper call-site return values to `$null` (`Invoke-VersionStamp`, `Invoke-MakeAppx`, `Write-PublishManifest` in stages 4/6). A captured invocation now emits exactly one pipeline object (the bundle root). Helper return behavior in `Publish.Msix.psm1` / `Publish.Helpers.psm1` is unchanged (verified via `git diff` returning no output).
- **New `scripts/Deploy.ps1` (AC-2 through AC-5):** a `[CmdletBinding(SupportsShouldProcess = $true)]` wrapper that runs `Publish.ps1`, captures the bundle root, fails fast on a throw or empty bundle root, then invokes the staged `<bundleRoot>\Install.ps1`, forwarding publish/install parameters (`-SkipSign` mapped to `-AllowUnsigned`), propagating `-WhatIf` to both child invocations, and never changing the caller's working directory. The child-invocation seam is two guarded script-scope wrapper functions (`Invoke-PublishScript`, `Invoke-InstallScript`), matching the existing `Test-IsElevatedAdmin` guard pattern in `scripts/Install.ps1` — no third production module was introduced.
- **Tests (AC-6):** `tests/scripts/Publish.Tests.ps1` gained one new expect-fail-then-pass regression test for the output-contract fix; `tests/scripts/Deploy.Tests.ps1` (new) covers parameter forwarding, the `-SkipSign` -> `-AllowUnsigned` mapping, publish-failure/empty-bundle-root short-circuit, `-WhatIf` propagation, the returned bundle root, and no-CWD-change, all via the wrapper-function mocking seam with no temp files.

### Verification
- Full PowerShell toolchain (format -> analyze -> test) passes clean repo-wide: 380 tests passed, 0 failed.
- Coverage (corrected-runsettings workaround, since the bundled MCP test tool's coverage path is repo-mismatched — known defect #111/#125/#135/#137): repo-wide 89.94% (baseline 89.93%, no regression); changed file `Publish.ps1` 97.47%; new file `Deploy.ps1` 87.10%. All above the 85% line / 75% branch-proxy floors.
- All 6 acceptance criteria checked off in `issue.md`, `user-story.md`, and `spec.md`.

Full evidence: `docs/features/active/2026-07-10-deploy-wrapper-entry-point-139/evidence/`.

---

PostedAs: comment
URL: https://github.com/drmoisan/open-claw-bridge/issues/139#issuecomment-4936924234
