# Remediation Inputs — Issue #144 (container-validation-stray-v1-and-env-target)

- Generated: 2026-07-11T00-45
- Source artifacts: `policy-audit.2026-07-11T00-45.md`, `code-review.2026-07-11T00-45.md`, `feature-audit.2026-07-11T00-45.md` (same folder)
- Overall verdict driving this remediation: **FAIL — 1 Blocking finding.**

## Remediation-Required Finding 1 (Blocking)

**Title:** Two new tests in `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` fail under a standard `Invoke-Pester` invocation; they pass only via the specific `Invoke-PoshQCTest` MCP-wrapper invocation path.

**Affected files:**
- `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` (lines 27-53, 55-81 — the two affected `It` blocks; lines 30 and 58 specifically contain the unscoped `Mock Get-OpenClawOperatorEnvFilePath` calls)
- `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1` (line 38 — the root-cause site; this file is pre-existing/shared and was not modified by this branch, but its latent defect is what this branch's new test-file style newly exposes)

**Independently verified evidence (this review):**
- Standard `Invoke-Pester -Configuration $config` (v5.6.1, plain `New-PesterConfiguration`, no code coverage, no MCP wrapper), run against `tests/scripts/Invoke-OpenClawContainerPathValidation.EnvFilePathDefault.Tests.ps1` alone: **0 passed / 2 failed**, `CommandNotFoundException: Could not find Command Get-OpenClawOperatorEnvFilePath`.
- The same plain invocation against the full `tests/scripts` suite: **414 passed / 2 failed** (same two tests; no other test affected).
- Diagnostic trace (`-Output Diagnostic`) confirms the mechanism: `Mock: Searching for command Get-OpenClawOperatorEnvFilePath in the script scope. Did not find command Get-OpenClawOperatorEnvFilePath in the script scope.`
- Root cause: `Import-OpenClawContainerValidationModule` (defined in `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1:31-39`) calls `Import-Module -Name $modulePath -Force -ErrorAction Stop` **without `-Global`**. Because this call executes from inside a function belonging to another module (`Fixtures.psm1`), the imported `OpenClawContainerValidation` module becomes a nested module of `Fixtures.psm1`; its exported functions are not re-exported to the caller's global/script scope. The affected test file's own `BeforeDiscovery` block does a separately, correctly-scoped `Import-Module -Force` first, but `BeforeAll`'s subsequent call to the nested-import fixture helper (needed for `Install-DefaultInvokeFakeDocker`, shared across every sibling test file) re-imports with `-Force`, replacing the earlier global registration with the nested one before the `It` blocks' unscoped `Mock` calls run.
- The executor's own committed test evidence (`evidence/qa-gates/final-poshqc-test.2026-07-10T20-30.md`, 416/416 passed) was captured exclusively via the `Invoke-PoshQCTest` MCP-wrapper invocation path, which this review empirically confirmed avoids the visibility bug through an implementation detail specific to that tool (its `-InvokePester` default scriptblock's own lexical/module-scope binding) — not a general property of `Invoke-Pester` itself.
- **Fix verified** (scratch reproduction only; no repository files were modified by this review — a temporary copy of the fixture with `-Global` added and a temporary copy of the test file pointing at it were created, tested, and deleted; `git status --porcelain` confirmed clean afterward): adding `-Global` to the `Import-Module` call at `Fixtures.psm1:38` makes both tests pass (2/2) under a completely standard `Invoke-Pester` invocation.

**Required remediation:**
1. Add `-Global` to the `Import-Module -Name $modulePath -Force -ErrorAction Stop` call in `tests/scripts/fixtures/OpenClawContainerValidation.Fixtures.psm1`'s `Import-OpenClawContainerValidationModule` function (currently line 38).
2. Re-run the full `tests/scripts` Pester suite via a standard, non-MCP-wrapper `Invoke-Pester` invocation (matching how CI/VS Code Test Explorer would run it) and confirm 416/416 (or the then-current total) pass with zero failures, in addition to re-confirming the existing MCP-wrapper coverage-mode run still passes.
3. Re-verify that the `-Global` change does not alter behavior for any other test file that uses the shared fixture (all other sibling test files use `-ModuleName`-scoped mocks, which are unaffected by whether the module is *also* globally visible; a full-suite pass count matching the pre-remediation coverage-mode figure, with the two previously-failing tests now passing under plain invocation, is the acceptance bar).
4. Re-capture the toolchain-loop evidence (`evidence/qa-gates/final-poshqc-test.<new-timestamp>.md` or equivalent) showing a standard `Invoke-Pester` full-suite pass, not solely the MCP-wrapper coverage-mode run, so future reviews are not dependent on this same tool-wrapper quirk to substantiate AC4.
5. Update `issue.md`'s AC3 and AC4 checkboxes only after the above is verified (they were independently found PARTIAL/FAIL by this review despite being marked `- [x]`; see `feature-audit.2026-07-11T00-45.md`).

**Policy basis:** `.claude/rules/powershell.md` ("Tests must produce identical results in Terminal and the VS Code Test Explorer... do not rely on ambient environment resolution"); `.claude/rules/general-unit-test.md` (Determinism, Isolation core principles).

**Disposition:** Blocking. Must be resolved before this branch is considered fully compliant.

## Non-Blocking Recommendations (for completeness; do not gate the current remediation cycle)

1. **Minor — stale documentation cross-references.** `docs/mailbridge-runbook.md` lines 445 and 457 (HostAdapter host-side setup walkthrough) retain `/v1/status`/`/v1`-suffixed references that are stale relative to HostAdapter's actual root-level routes (independently confirmed via `src/OpenClaw.HostAdapter/Program.cs`). Recommend a follow-up documentation fix; out of this branch's AC1/AC2/AC6 literal scope, so non-blocking.
2. **Info — repo-wide PowerShell coverage convention gap.** The established `scripts/*.ps1`/`scripts/*.psm1` "repo-wide" coverage glob convention does not recurse into `scripts/powershell/modules/**`, silently excluding this branch's own `OpenClawContainerValidation.psm1` (and the pre-existing `OpenClawRbac` module) from any repo-wide figure. Pre-existing tooling gap, not introduced by this branch; recommend a follow-up to widen the convention via recursive `Get-ChildItem` enumeration. Does not affect this branch's own per-file compliance (independently confirmed PASS).

## Reference Artifact Paths

- `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/policy-audit.2026-07-11T00-45.md`
- `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/code-review.2026-07-11T00-45.md`
- `docs/features/active/2026-07-10-container-validation-stray-v1-and-env-target-144/feature-audit.2026-07-11T00-45.md`
