---
name: poshqc-test-wrapper-masks-mock-scope-bugs
description: Invoke-PoshQCTest's internal invocation path can mask a real, deterministic Pester Mock-scope failure that a plain Invoke-Pester run reproduces every time — always cross-check new tests with a plain Invoke-Pester run, not just the MCP-wrapper/corrected-runsettings path
metadata:
  type: project
---

On issue #144 (2026-07-10/11, container-validation-stray-v1-and-env-target, minor-audit,
open-claw-bridge, PowerShell-only): the executor's committed test evidence (416/416 passed) was
captured exclusively via `Invoke-PoshQCTest` (the drm-copilot MCP wrapper's corrected-runsettings
workaround for the already-known `#111`/`#125`/.../`#142` bundled-runsettings defect — see
[[review-env-fallbacks]]). A plain `Invoke-Pester -Configuration $config` run (no coverage, no
wrapper, `New-PesterConfiguration` built either via property assignment or `-Hashtable`) against
the exact same test file reproducibly FAILED 2 of its tests, both standalone and full-suite,
with `CommandNotFoundException: Could not find Command <ExportedModuleFunction>`.

**Root cause:** a shared test fixture helper function (defined in ANOTHER module, e.g.
`OpenClawContainerValidation.Fixtures.psm1`) called `Import-Module -Force` (no `-Global`) on the
production module. Because the import executes from inside a function belonging to a different
module, the imported module becomes a NESTED module of the fixture, and its exported functions are
not re-exported to the caller's global/script scope. An unscoped `Mock <FunctionName>` (no
`-ModuleName`) in the `.Tests.ps1` file then cannot resolve the command via `Get-Command` in
"script scope" (confirmed via `Invoke-Pester -Output Diagnostic`, which prints exactly
`Mock: Searching for command X in the script scope. Did not find command X in the script scope.`).
This is a LATENT defect in the shared fixture — every other test file in the suite used
`-ModuleName`-scoped mocks and never hit it; only this branch's NEW test-file style (unscoped
`Mock` against an exported function from a different module) exposed it.

**Why the MCP wrapper hid it:** `Invoke-PoshQCTest`'s default `-InvokePester` scriptblock parameter
is DEFINED inside `PoshQC.Testing.psm1` and invoked via `& $InvokePester $config` — this specific
scriptblock's own lexical/module-scope binding happens to avoid the visibility bug (empirically
confirmed: injecting an IDENTICAL custom `-InvokePester = { param($Config) Invoke-Pester
-Configuration $Config }` from the CALLER's own top-level session reproduces the failure again,
while the wrapper's unmodified default scriptblock does not). Enabling/disabling CodeCoverage,
building the config via `-Hashtable` vs. property assignment, and pinning the Pester version were
all ruled out as the differentiator via direct A/B testing — the ONLY variable that flipped the
result was whether `Invoke-Pester` was invoked via the wrapper's own default scriptblock closure
versus any equivalent call made from the reviewer's own session.

**Fix verified (scratch-only, no repo files modified for the finding itself):** adding `-Global`
to the fixture's `Import-Module -Force` call makes the tests pass under a completely plain
`Invoke-Pester` run. Verified via a temporary scratch copy of both the fixture (renamed) and the
test file (pointed at the renamed fixture), placed briefly inside `tests/scripts/` (since
`$PSScriptRoot`-relative path resolution inside the files requires them to live in the real tree),
then deleted immediately after confirming pass/fail; `git status --porcelain` confirmed clean
afterward.

**How to apply:** whenever `run_poshqc_test`/`Invoke-PoshQCTest` is the only test-execution path
available (common in this repo per [[review-env-fallbacks]]), do NOT treat its pass/fail count as
sufficient proof that a NEW test file is environment-independent — especially when that new test
file introduces an unscoped `Mock <FunctionName>` pattern not previously used elsewhere in the
suite (existing tests in this repo consistently use `-ModuleName`-scoped mocks for module-exported
functions). Always additionally re-run at least the new/changed test files (and ideally the full
suite) via a bare `Invoke-Pester -Configuration (New-PesterConfiguration)` with `Run.Path` set
directly, with NO coverage settings and NO MCP wrapper, before accepting a "full suite passes"
claim. If a discrepancy appears, check for unscoped `Mock` calls against functions imported via a
shared fixture helper that itself lives in a different module, and check whether that helper's
`Import-Module` call is missing `-Global`.

This is graded as a BLOCKING finding (not Minor/Info) because it directly contradicts an explicit
AC requiring "the full suite passes... in a single pass," and because
`.claude/rules/powershell.md` explicitly requires test-environment independence ("must produce
identical results in Terminal and the VS Code Test Explorer... do not rely on ambient environment
resolution") — this is squarely that class of defect, just discovered via an MCP-wrapper-vs-plain
comparison rather than a Terminal-vs-Test-Explorer one.

Newest artifact set demonstrating this quirk (FAIL-verdict, Blocking + remediation-inputs,
PowerShell-only minor-audit): `2026-07-10-container-validation-stray-v1-and-env-target-144/
*.2026-07-11T00-45.md`.
