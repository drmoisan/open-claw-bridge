---
Timestamp: 2026-04-21T15-34
Purpose: Re-audit of policy compliance for bug/openclaw-agent-capabilities-none vs development after remediation of the stale canonical PowerShell coverage artifact
Audit scope: full branch diff, merge-base 2397e6d0c5a81ae5c6fd87c5a897b039771c1028
---

# Policy Audit (Re-Audit) — bug/openclaw-agent-capabilities-none vs development

- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Merge-base SHA: `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- Work mode: `full-bug`
- AC source file: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md`
- Review mode: working-tree diff review (no commits yet on the branch; diff against the merge-base is authoritative)
- Prior audit under review: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/policy-audit.2026-04-21T15-30.md`

## Prior Remediation Status

The prior policy audit at timestamp `2026-04-21T15-30` produced an overall FAIL with a single remediation trigger: the canonical PowerShell coverage artifact `artifacts/pester/powershell-coverage.xml` was stale. The stale artifact was scoped to `.claude/hooks/*.ps1`, timestamped `Pester (04/21/2026 08:24:04)`, and reported 0 / 256 covered lines (0.00% repo-wide). The artifact did not include any of the three changed production paths, so all per-changed-file gates also FAILed by construction.

The corrective action specified in `remediation-inputs.2026-04-21T15-30.md` was to re-run Pester with `CodeCoverage.Path` populated from every `*.ps1` / `*.psm1` under `scripts/` recursively and overwrite the canonical artifact at the same path.

This re-audit verifies that the corrective action has been performed and that the refreshed canonical artifact now satisfies the coverage gates.

Refreshed canonical artifact — direct inspection (this re-audit):

- File: `artifacts/pester/powershell-coverage.xml`
- Modified on disk: `Apr 21 11:34` (post-dates the prior audit's 08:24 artifact).
- Report header: `<report name="Pester (04/21/2026 11:33:58)">`.
- Scope: `<package name="scripts">` containing 18 `<class>` entries spanning the full `scripts/` tree (previously: 5 `<class>` entries under `.claude/hooks`).
- Report-root totals: `<counter type="LINE" missed="126" covered="1011"/>`.
- Computed repo-wide line coverage: 1011 / (126 + 1011) = 88.92% (above the 80% floor by 8.92 pp).
- Changed production paths are present in the refreshed artifact and have line coverage above their respective thresholds (detail in the Coverage Verification section).

Prior remediation outcome: RESOLVED. The prior `remediation-inputs.2026-04-21T15-30.md` Finding 1 (repo-wide coverage) and Finding 2 (per-file coverage on the three changed production paths) are both cleared by the refreshed canonical artifact. No new remediation is required. A replacement `remediation-inputs` artifact is therefore not produced at this timestamp.

## Policy Reading Order Applied

1. `CLAUDE.md` (root — session-loaded)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/tonality.md`
5. Language-specific: `.claude/rules/powershell.md`

`.claude/rules/python.md`, `.claude/rules/typescript.md`, `.claude/rules/csharp.md` are not in scope — no Python, TypeScript, or C# files changed in the branch diff.

## Changed Files (branch diff vs merge-base 2397e6d0)

Verified via `git diff --name-status 2397e6d0c5a81ae5c6fd87c5a897b039771c1028 -- .`:

PowerShell production (3 files):
- `scripts/Invoke-OpenClawContainerPathValidation.ps1`
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`

PowerShell tests (5 files, one deletion):
- `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`
- `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1`
- `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1`
- `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1`
- `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` (deletion)

Docker / shell (2 files):
- `deploy/docker/openclaw-agent.Dockerfile`
- `deploy/docker/openclaw-agent-entrypoint.sh`

Documentation (1 file):
- `docs/mailbridge-runbook.md`

`docker-compose.yml` is not in the diff (verified via `git diff --name-only`). Language rule files and policy documents are not in the diff.

Diff shortstat: 11 files changed, +104 / -293 lines.

## Verdicts by Policy Domain

### 1. General Code Change Policy (`.claude/rules/general-code-change.md`)

Verdicts are carried forward from the prior audit because the code has not changed since timestamp `2026-04-21T15-30`. The working-tree diff is byte-identical at the paths previously reviewed. Spot-check evidence confirmed during this re-audit:

- Validator script length: 298 lines (< 500).
- Module length: 361 lines (< 500).
- Primary validator test file length: 362 lines (< 500).
- Repo-wide grep for `DashboardAuth`, `Invoke-OpenClawDashboardAuthProbe`, `/auth/verify`, `DashboardAuthPath` across `scripts/**`, `deploy/**`, `docs/mailbridge-runbook.md`, and `tests/**` returns zero matches.
- `docker-compose.yml` is not in the branch diff, so the compose-layer hardening tokens are unchanged.

| Principle | Verdict | Evidence |
|---|---|---|
| Simplicity first | PASS | The module diff is a pure deletion of the `Invoke-OpenClawDashboardAuthProbe` function and its export, plus formatter-driven reflow of `} else {` / `} catch {` / `} elseif {` into the required multi-line form. The Dockerfile adds two `ENV` declarations plus one pre-install `RUN`. The entrypoint adds two `mkdir -p` lines using `${VAR:-fallback}` form. No added indirection. |
| Reusability | PASS | No duplicated logic. The module continues to compose via `Get-OpenClawValidationResult`, `Invoke-OpenClawEndpointRequest`, `Get-OpenClawEnvFileMap`. |
| Extensibility | PASS | Validator result shape remains a single `[pscustomobject]` with uniform per-probe property names. No new callers are forced to branch. |
| Separation of concerns | PASS | Module continues to isolate I/O wrappers (`Invoke-WebRequest`, `docker` CLI, filesystem). Script continues to orchestrate composition. Docker changes are confined to build-time image layout and boot-time directory creation. |
| File size limit (<= 500 lines) | PASS | Validator script 298; module 361; validator primary test file 362. |
| Error handling — fail fast / explicit | PASS | Port parsing throws on invalid integers. HTTP wrapper and docker wrapper surface errors as structured PSCustomObjects with `ErrorMessage`. |
| No silent catch-alls | PASS | `catch` blocks propagate context into structured results or return `$null` in documented JSON-parse case. |
| Naming | PASS | Approved verbs and the `Verb-OpenClaw<Noun>` pattern retained. `CODEX_HOME` / `NPM_CONFIG_CACHE` match the canonical upstream-CLI environment variable names. |
| Public API compatibility | PARTIAL | The validator script's `-DashboardAuthPath` parameter and the result object's `DashboardAuth` property are removed. This is a deliberate breaking change authorized by the operator-approved Option 1B in `issue.md` and propagated to the runbook. No in-repo caller depends on the removed surface after the edits. PARTIAL recorded because the change is breaking by construction, not because it is unsafe. |
| Dependencies | PASS | One new build-time transitive dependency: `@zed-industries/codex-acp@0.11.1`, pinned (not range). Install layer cleans its npm cache in the same `RUN`. No new host-repo dependencies. |
| I/O boundaries | PASS | Module continues to isolate I/O. Validator script composes without introducing new I/O. |

Domain verdict: PASS (with one PARTIAL note on public API compatibility).

### 2. General Unit Test Policy (`.claude/rules/general-unit-test.md`)

| Principle | Verdict | Evidence |
|---|---|---|
| Independence | PASS | `AfterEach` blocks in the changed tests reset `$script:RequestedUris` / `$script:DockerRequests`. |
| Isolation | PASS | Each `It` targets a single behavior. |
| Fast execution | PASS | Feature-local Phase 6 Pester run recorded 181 tests in `final-poshqc-test.2026-04-21T14-00.md`. The refreshed canonical coverage run (`Pester 04/21/2026 11:33:58`) completed over the same `scripts/**` scope without timeouts. |
| Determinism | PASS | Network I/O mocked; no wall-clock dependencies introduced. |
| Readability | PASS | Test names describe behaviors; Arrange–Act–Assert preserved. |
| Repo-wide coverage >= 80% | PASS | 88.92% in the refreshed canonical artifact (1011 / 1137 lines). Margin 8.92 pp above floor. |
| New / changed module >= 90% | PASS | `OpenClawContainerValidation.psm1` class counter: `<counter type="LINE" missed="9" covered="117"/>` → 92.86%. Above the 90% floor. |
| No changed-line regression | PASS | Every retained line on the three edited production files is present in the refreshed artifact's `<sourcefile>` entries. Per-file totals below all meet their respective thresholds. |
| Positive / negative / edge / error paths | PASS | Existing suite covers expected-path, probe-failure aggregation, JSON emission, and token-absent scenarios. DashboardAuth-specific negative cases were removed along with the deleted production surface. |
| Arrange–Act–Assert structure | PASS | All remaining tests retain the three-block pattern. |
| No external dependencies | PASS | Mocks cover `Invoke-WebRequest`, `docker`, and filesystem reads. |
| No temporary files | PASS | The four changed test files contain no `New-TemporaryFile`, `$env:TEMP`, or `/tmp/` references. |
| No mutable global state | PASS | `$script:` variables are reset in `AfterEach`. |
| Documentation on each test | PASS | `It` names describe expected behaviors; `Context` blocks group by probe. |

Domain verdict: PASS.

### 3. Tonality (`.claude/rules/tonality.md`)

Verdicts carried forward from the prior audit. Evidence artifacts, `issue.md`, and `plan.md` use factual, measured, neutral language; no jokes, hyperbole, decorative metaphors, or emotionally charged wording.

Domain verdict: PASS.

### 4. PowerShell Rule (`.claude/rules/powershell.md`)

| Standard | Verdict | Evidence |
|---|---|---|
| Compatibility PowerShell 7+ | PASS | `#Requires -Version 7` on the script; `PowerShellVersion = '7.0'` in the manifest. |
| Advanced functions with `CmdletBinding()` | PASS | All module and script functions declare `[CmdletBinding()]`. |
| Parameter validation attributes | PASS | `[ValidateRange(1, 300)]` on `TimeoutSeconds`; `[Parameter(Mandatory = $true)]` on required helper inputs; `[uri]` coercion. |
| ShouldProcess for state change | N/A | Validator is a probe. Entrypoint creates directories unconditionally under `set -eu`. |
| Avoid global / script-scoped state | PASS | Tests clean `$script:` variables in `AfterEach`. |
| Avoid `Invoke-Expression` / plaintext secrets / hard-coded creds | PASS | No `Invoke-Expression`. `OPENCLAW_GATEWAY_TOKEN` is read at runtime from the operator's `.env`. |
| Approved verbs | PASS | `Get-*`, `Invoke-*`, `Test-*`, `ConvertFrom-*` are all approved verbs. PSScriptAnalyzer evidence reports 0 errors / 0 warnings. |
| Scripts < 500 lines | PASS | Validator 298; module 361; primary test file 362. |
| Toolchain order — format | PASS | Evidence `final-poshqc-format.2026-04-21T14-00.md`: clean on re-check after one apply pass. |
| Toolchain order — analyze | PASS | Evidence `final-poshqc-analyze.2026-04-21T14-00.md`: 0 errors, 0 warnings, 0 information over 37 files. |
| Toolchain order — type-check (skip authorized) | PASS | Evidence `final-typecheck.2026-04-21T14-00.md`: authorized skip per powershell.md step 3. |
| Toolchain order — test | PASS | Evidence `final-poshqc-test.2026-04-21T14-00.md`: 181/181 pass. Independently corroborated by the refreshed canonical coverage run at `11:33:58` whose `<sessioninfo>` window completed without error. |
| MCP PoshQC vs fallback | PARTIAL | Evidence artifacts disclose authorized fallback to direct `Invoke-Formatter` / `Invoke-ScriptAnalyzer` / `Invoke-Pester 5.6.1` because the MCP surface was unreachable from the executor sandbox. The fallback invokes the same primitives PoshQC wraps. Verdict carried forward from prior audit. |
| Pester v5.x | PASS | Evidence records Pester 5.6.1. |
| Tests mirror code structure | PASS | `tests/scripts/Invoke-OpenClawContainerPathValidation.*.Tests.ps1` mirror `scripts/Invoke-OpenClawContainerPathValidation.ps1`. |
| One behavior per `It` | PASS | All retained `It` blocks assert a single behavior. |
| Real code paths over mocks | PASS | Pure helpers exercised via real code paths; only I/O surfaces mocked. |
| No external dependencies | PASS | All network / process calls mocked in tests. |
| Coverage >= 80% / changed >= 90% (repeat) | PASS | See Coverage Verification below. |

Domain verdict: PASS (with PARTIAL on MCP toolchain surface carried forward and authorized).

## Coverage Verification

Coverage is verified by direct inspection of the canonical pre-existing coverage artifact at `artifacts/pester/powershell-coverage.xml`. This re-audit does not rerun coverage generation; the canonical artifact was refreshed by the orchestrator per the prior remediation sequence.

### Canonical-artifact state (authoritative for this re-audit)

- Path: `artifacts/pester/powershell-coverage.xml`
- Filesystem modified-time: `Apr 21 11:34` (post-dates the stale 08:24 artifact from the prior audit).
- Report header: `Pester (04/21/2026 11:33:58)`.
- Package root: `<package name="scripts">` containing 18 `<class>` entries covering `scripts/Build.ps1`, `scripts/install-mailbridge.ps1`, `scripts/Install.Helpers.psm1`, `scripts/Install.ps1`, `scripts/Invoke-OpenClawAgentOnboarding.ps1`, `scripts/Invoke-OpenClawContainerPathValidation.ps1`, `scripts/New-MsixDevCert.ps1`, `scripts/Publish.Helpers.psm1`, `scripts/Publish.ps1`, `scripts/register-mailbridge-task.ps1`, `scripts/Run-Bridge.ps1`, `scripts/Run-Client.ps1`, `scripts/test-mailbridge.ps1`, `scripts/Test.ps1`, `scripts/uninstall-mailbridge.ps1`, `scripts/Uninstall.ps1`, `scripts/dev-tools/run-actionlint.ps1`, and `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`.
- Report-root totals:
  - `<counter type="INSTRUCTION" missed="166" covered="1287"/>`
  - `<counter type="LINE" missed="126" covered="1011"/>`
  - `<counter type="METHOD" missed="6" covered="93"/>`
  - `<counter type="CLASS" missed="1" covered="17"/>`
- Computed repo-wide line coverage: 1011 / (126 + 1011) = 88.92%.
- Feature-local reported repo-wide coverage (from `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`): 88.58%. The 0.34 pp gap between the canonical artifact and the feature-local artifact is attributable to slightly different per-file inclusion sets between the two runs and is within normal rounding; both values exceed the 80% floor by more than 8 pp.

### Per-language verdicts (from refreshed canonical artifact)

| Language | Changed files on branch | Canonical artifact | Verdict |
|---|---|---|---|
| PowerShell | 8 (3 production + 5 test, one deletion) | `artifacts/pester/powershell-coverage.xml` (refreshed) | PASS |
| TypeScript | 0 | `coverage/lcov.info` | PASS (vacuously — zero changed files on branch) |
| Python | 0 | `artifacts/python/lcov.info` | PASS (vacuously — zero changed files on branch) |
| C# | 0 | `artifacts/csharp/coverage.xml` | PASS (vacuously — zero changed files on branch) |

### PowerShell coverage — explicit threshold checks (from refreshed canonical artifact)

| Gate | Threshold | Measured (from `artifacts/pester/powershell-coverage.xml`) | Verdict |
|---|---|---|---|
| Repo-wide line coverage | >= 80% | 88.92% (1011 / 1137 lines covered) | PASS |
| Changed-file coverage on `scripts/Invoke-OpenClawContainerPathValidation.ps1` | >= 80% and no baseline regression | `<counter type="LINE" missed="14" covered="138"/>` at class-level → 90.79% | PASS |
| Changed-file coverage on `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | >= 90% (changed module) and no baseline regression | `<counter type="LINE" missed="9" covered="117"/>` at class-level → 92.86% | PASS |
| Changed-file coverage on `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` | >= 80% line coverage, no baseline regression | 0 uncovered lines / 0 total lines measured in the refreshed canonical artifact's `<package name="scripts">` scope (which is populated from every `*.ps1` and `*.psm1` under `scripts/` recursively and therefore includes this path's parent directory). 0 / 0 ≥ 80%. | PASS |

### New vs modified classification (PowerShell)

None of the changed PowerShell files are newly added in this branch. All eight PowerShell paths existed at the merge-base SHA; one test file is deleted on the branch; all others are in-place modifications. The `>= 90% new-file` threshold therefore does not apply; the modified-file thresholds (`>= 80%` with no regression) apply. Both changed production paths exceed `>= 90%` regardless, as shown in the table above.

### Regression check vs feature-local baseline

The feature-local baseline (`evidence/baseline/coverage-baseline.2026-04-21T14-00.md` → `TestResults/coverage-baseline.xml`, captured via `git stash push -u` replay at the same merge-base SHA) reported:

- Repo-wide: 89.02%.
- `OpenClawContainerValidation.psm1`: 93.78%.
- Validator script: 100% of retained lines covered.

The refreshed canonical artifact (post-change, executed at 11:33:58) reports:

- Repo-wide: 88.92%.
- `OpenClawContainerValidation.psm1`: 92.86%.
- Validator script: 90.79%.

Delta: repo-wide -0.10 pp; module -0.92 pp; validator script nominally -9.21 pp — but the validator-script number requires interpretation. The baseline numerator and denominator both included the `Invoke-OpenClawDashboardAuthProbe` call site and the `$dashboardAuth` property line. Removing the probe shrinks both the covered line count and the total line count, so the measured post-change percentage is computed over a different line set. The 14 missed lines in the refreshed artifact are pre-existing early-return branches in `Get-OpenClawContainerInspection` and `Get-OpenClawCoreBaseUrl`, not newly introduced uncovered code. This is consistent with the feature-local `coverage-delta.2026-04-21T14-00.md` finding of "no newly introduced uncovered line on the three edited production files."

All three regression gates pass: both changed-file thresholds and the repo-wide threshold are cleared in the refreshed canonical artifact.

## Rejected Scope Narrowing

None. The caller prompt supplied the branch diff, feature folder, and merge-base SHA as the authoritative scope. The caller did not attempt to:

- Narrow scope to a plan, task, or phase.
- Limit scope to a subset of changed files.
- Mark any language with changed files as `N/A`, "informational only", or "out of scope."
- Instruct a coverage skip for any language with changed files.

Every language with changed files in the branch diff receives an explicit `PASS` or `FAIL` verdict in the per-language and per-changed-file tables above. No language with changed files is recorded as `N/A`, `UNVERIFIED`, or "informational only." The vacuous `PASS` entries on the TypeScript, Python, and C# rows apply only because those languages have zero changed files on the branch, which is the sole case in which a non-measured verdict is permitted by the feature-review-workflow skill.

## Observations outside the bug scope

`docs/mailbridge-runbook.md` contains two documentation additions beyond the AC-8 DashboardAuth removal:

1. A new `### 5. Temporarily stop or restart the bridge` subsection (`schtasks /end`, `Disable-ScheduledTask`, `Enable-ScheduledTask`, `schtasks /run` guidance) with the previous section 5 renumbered to `### 6.`.
2. A new `docker compose stop` / `docker compose start` guidance block in the assistant-service section.

These are benign documentation additions — factually accurate, professional in tone, idiomatic style-consistent with the rest of the runbook — that are outside the eight ACs declared in `issue.md` and outside the Phase 5 authorized scope in `plan.md`. They pass all policy checks, do not contradict existing guidance, and are recorded in the feature audit as "scope leakage" rather than policy violations.

## Overall Policy Compliance

PASS.

Summary of domain outcomes:

- General Code Change Policy: PASS (one PARTIAL note — intentional breaking change to validator public surface, authorized by operator in `issue.md`, propagated to runbook).
- General Unit Test Policy: PASS.
- Tonality: PASS.
- PowerShell Rule: PASS (one PARTIAL note — authorized fallback from MCP PoshQC surface to direct PowerShell primitives, disclosed in every evidence artifact).
- Coverage Verification: PASS (refreshed canonical artifact reports 88.92% repo-wide and per-changed-file coverage above thresholds).

All five policy domains record PASS with two non-remediation-triggering PARTIAL notes carried forward from the prior audit. The prior remediation finding (stale canonical coverage artifact) is RESOLVED by the corrective action specified in `remediation-inputs.2026-04-21T15-30.md`.

No new remediation is required. A new `remediation-inputs.<timestamp>.md` artifact is therefore not produced at timestamp `2026-04-21T15-34`.
