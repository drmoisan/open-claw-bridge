---
Timestamp: 2026-04-21T15-30
Purpose: Policy compliance audit for the bug/openclaw-agent-capabilities-none branch vs development
Audit scope: full branch diff, merge-base 2397e6d0c5a81ae5c6fd87c5a897b039771c1028
---

# Policy Audit — bug/openclaw-agent-capabilities-none vs development

- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Merge-base SHA: `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- Work mode: `full-bug`
- AC source file: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md`
- Review mode: working-tree diff review (no commits yet on the branch; no PR context bundle available)

## Policy Reading Order Applied

1. `CLAUDE.md` (root — session-loaded)
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/tonality.md`
5. Language-specific: `.claude/rules/powershell.md`

`.claude/rules/python.md`, `.claude/rules/typescript.md`, `.claude/rules/csharp.md` are not in scope — no Python, TypeScript, or C# files changed in the branch diff.

## Changed Files (branch diff vs merge-base 2397e6d0)

PowerShell (5 files):
- `scripts/Invoke-OpenClawContainerPathValidation.ps1` (production)
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` (production)
- `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` (production)
- `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` (test)
- `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` (test)
- `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1` (test)
- `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1` (test)
- `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` (test — deletion)

Docker / shell (2 files):
- `deploy/docker/openclaw-agent.Dockerfile`
- `deploy/docker/openclaw-agent-entrypoint.sh`

Documentation (1 file):
- `docs/mailbridge-runbook.md`

No changes to `docker-compose.yml`, language rule files, or policy documents.

## Verdicts by Policy Domain

### 1. General Code Change Policy (`.claude/rules/general-code-change.md`)

| Principle | Verdict | Evidence |
|---|---|---|
| Simplicity first | PASS | The validator module diff removes a probe whose default path (`/auth/verify`) was documented as unverified. The Dockerfile adds two `ENV` declarations and a single pre-install `RUN`. The entrypoint adds two `mkdir -p` lines with `${VAR:-fallback}` fallback form. All edits are linear, no added indirection. |
| Reusability | PASS | No duplicated logic introduced. The module continues to use the shared `Get-OpenClawValidationResult`, `Invoke-OpenClawEndpointRequest`, `Get-OpenClawEnvFileMap` helpers. |
| Extensibility | PASS | The validator's `[pscustomobject]` result remains keyword-style; the removed `DashboardAuth` property is the only observable surface change. No public API is extended in a way that forces callers to branch. |
| Separation of concerns | PASS | Pure helpers remain in the module; the script continues to own the orchestration. The Docker changes are confined to build-time image composition (Dockerfile) and boot-time directory creation (entrypoint); no application logic was added. |
| Create class/function when appropriate | PASS | No new classes added. The removed `Invoke-OpenClawDashboardAuthProbe` function was removed in full (per AC-3). The remaining functions continue to use advanced-function form with `CmdletBinding()` and named parameters. |
| Small focused methods | PASS | All remaining module functions stay below 50 lines; the validator script's top-level orchestration is ~60 lines. |
| Interfaces/protocols for polymorphism | N/A | PowerShell module; uniform PSCustomObject shape is maintained via `Get-OpenClawValidationResult`. |
| File size limit (<= 500 lines) | PASS | Post-edit line counts — `Invoke-OpenClawContainerPathValidation.ps1` 298; `OpenClawContainerValidation.psm1` 361; `Invoke-OpenClawContainerPathValidation.Tests.ps1` 362. All three shrunk vs baseline due to the probe removal. |
| Error handling — fail fast / explicit | PASS | Port parsing in `Get-OpenClawCoreBaseUrl` throws on invalid integers. Docker command wrapper and HTTP wrapper return structured PSCustomObjects with `ErrorMessage` fields rather than swallowing. |
| No silent catch-alls | PASS | The two remaining `catch` blocks in the module (`Invoke-OpenClawEndpointRequest`, `Invoke-OpenClawDockerCommand`, `ConvertFrom-OpenClawJsonContent`) either propagate the exception message into a structured result or return `$null` in the JSON-parse case (which is documented in the function synopsis). |
| Appropriate logging levels | PASS | No new logging introduced; existing `Write-Output`/`Format-Table` contract on the CLI-friendly branch retained. |
| Invariants at construction | PASS | `[ValidateRange(1, 300)]` on `TimeoutSeconds`, `[uri]` coercion on base URLs, `[int]::TryParse` guard on port parsing. |
| Assertions vs user errors | PASS | No assertions added; user errors are surfaced via `throw` with message. |
| Naming | PASS | PowerShell approved verbs and `Verb-OpenClaw<Noun>` pattern retained. `CODEX_HOME` / `NPM_CONFIG_CACHE` are canonical environment-variable names expected by the upstream `codex`/`npm` CLIs. |
| Public API compatibility | PARTIAL | The validator script's `-DashboardAuthPath` parameter and the result object's `DashboardAuth` property are removed. This is a breaking change to the script's public surface. It is authorized by the issue (Option 1B was operator-approved) and documented in `issue.md` / `plan.md`. `docs/mailbridge-runbook.md` is updated so callers are not directed at the removed surface. No in-repo caller retained a dependency on `-DashboardAuthPath` or `$result.DashboardAuth` after the edits. Verdict downgraded to PARTIAL only because this is a breaking change by construction, not because it is unsafe. |
| Dependencies | PASS | One new transitive dependency added at image build time: `@zed-industries/codex-acp@0.11.1`. This is pinned (not `^0.11.1`, not `latest`), it is the package already required by the upstream gateway's embedded ACP runtime (see baseline `agent-container-state` evidence), and the image layer cleans its caches after install. No new PowerShell modules, NuGet packages, pip packages, or npm packages in the host repo tree. |
| I/O boundaries | PASS | Module continues to isolate `Invoke-WebRequest`, `docker` CLI, and filesystem reads into wrapper functions. Validator script composes behavior from those wrappers without introducing new I/O. |

Domain verdict: PASS (with one PARTIAL noted on public API compatibility).

### 2. General Unit Test Policy (`.claude/rules/general-unit-test.md`)

| Principle | Verdict | Evidence |
|---|---|---|
| Independence | PASS | `AfterEach` in the changed tests clears `$script:RequestedUris` / `$script:DockerRequests`. Mocks are scoped with `-ModuleName OpenClawContainerValidation`. |
| Isolation | PASS | Each `It` targets one behavior (`returns expected when all container endpoints match their validation contracts`, `emits JSON when requested`, etc.). |
| Fast execution | PASS | 181 tests ran to completion via the baseline-capture Pester invocation. Network I/O is mocked through `Invoke-WebRequest`. |
| Determinism | PASS | Tests use mocked `Invoke-WebRequest`, mocked `docker`, mocked `Get-Content`. No wall-clock dependencies introduced. |
| Readability | PASS | Test names describe behaviors; Arrange-Act-Assert structure retained. |
| Repo-wide coverage >= 80% | PASS | Post-change 88.58% (evidence `coverage-delta.2026-04-21T14-00.md`). Baseline 89.02%. Delta -0.44 pp, floor 80% met with 8.58 pp margin. |
| New/changed module >= 90% | PASS | `OpenClawContainerValidation.psm1` post-change 90.80% (evidence `coverage-delta.2026-04-21T14-00.md`). Baseline 93.78%. Delta -2.98 pp, floor 90% met with 0.80 pp margin. |
| No changed-line regression | PASS | Per `coverage-delta.2026-04-21T14-00.md`: every retained line on the three edited production files is covered by at least one surviving test. The 15 missed module commands are pre-existing early-return branches, not newly introduced uncovered lines. |
| Positive / negative / edge / error paths | PASS | Existing suite already covers expected-path, one-probe-failure, multiple-probe-failure, JSON emission, and token-absent scenarios. The DashboardAuth-specific negative/401 cases were removed along with the production probe. |
| Arrange-Act-Assert structure | PASS | All remaining tests preserve the three-block pattern. |
| No external service dependencies | PASS | Mocks cover `Invoke-WebRequest`, `docker`, filesystem reads. |
| No temporary files | PASS | Searched the four changed test files for `New-TemporaryFile`, `$env:TEMP`, `/tmp/`; no matches inside the test files themselves. The evidence-reconstruction scripts used `/tmp/pester-post.ps1` etc. but those are agent-harness temp files outside the repo tree. |
| No mutable global state | PASS | `$script:` variables in tests are reset in `AfterEach` and cleared with `Remove-Variable`. |
| Documentation on each test | PASS | `It` names describe expected behavior; Context blocks group by probe. |

Domain verdict: PASS.

### 3. Tonality (`.claude/rules/tonality.md`)

| Constraint | Verdict | Evidence |
|---|---|---|
| Professional tone in evidence artifacts | PASS | Reviewed `evidence/baseline/*.md`, `evidence/qa-gates/*.md`, `issue.md`, `plan.2026-04-21T14-00.md`. All artifacts use factual, measured, neutral language. |
| No jokes/banter | PASS | No playful or comedic phrasing found. |
| No hyperbole | PASS | No "perfect", "amazing", "revolutionary" language. Result statements say "PASS" with a concrete measurement. |
| Metaphors tightly restricted | PASS | No decorative metaphors observed. |
| Evidence-first wording | PASS | Every claim in `issue.md` and `plan.md` references a timestamped evidence artifact or a concrete command output. |
| Difficult messages handled directly | PASS | Scope amendments in `plan.md` ("Phase 4B", "Phase 4C") state root cause, impact, and fix without blame-oriented wording. |

Domain verdict: PASS.

### 4. PowerShell Rule (`.claude/rules/powershell.md`)

| Standard | Verdict | Evidence |
|---|---|---|
| Compatibility PowerShell 7+ | PASS | `#Requires -Version 7` on the script. `PowerShellVersion = '7.0'` in the manifest. |
| Advanced functions with `CmdletBinding()` | PASS | All module functions and script functions declare `[CmdletBinding()]`. |
| Parameter validation attributes | PASS | `[ValidateRange(1, 300)]` on `TimeoutSeconds`. `[Parameter(Mandatory = $true)]` on required helper inputs. `[uri]` coercion for URLs. |
| ShouldProcess for state change | N/A | No state-changing actions in the script; the validator is a probe. The entrypoint script creates directories unconditionally, consistent with boot-time setup (`set -eu`). |
| Avoid global/script-scoped state | PASS | Module uses local function variables; tests clean up `$script:` variables in `AfterEach`. |
| Avoid `Invoke-Expression` / plaintext secrets / hard-coded creds | PASS | No `Invoke-Expression`. `OPENCLAW_GATEWAY_TOKEN` is read from the operator's `.env` at runtime via `Get-OpenClawEnvFileMap`. |
| Approved verbs | PASS | `Get-*`, `Invoke-*`, `Test-*`, `ConvertFrom-*` all approved. PSScriptAnalyzer 0 errors / 0 warnings confirms. |
| Scripts < 500 lines | PASS | Validator script 298 lines; module 361 lines; primary test file 362 lines. |
| Toolchain order — format | PASS | Evidence `final-poshqc-format.2026-04-21T14-00.md`: formatter terminated clean on re-check (pass 1 flagged 2 files, pass 2 rewrote, pass 3 re-check clean). The rule's "restart on autofix" directive was followed. |
| Toolchain order — analyze | PASS | Evidence `final-poshqc-analyze.2026-04-21T14-00.md`: 0 errors, 0 warnings, 0 information over 37 files. Zero delta vs baseline. |
| Toolchain order — type-check (skip authorized) | PASS | Evidence `final-typecheck.2026-04-21T14-00.md`: authorized skip per powershell.md step 3. |
| Toolchain order — test | PASS | Evidence `final-poshqc-test.2026-04-21T14-00.md`: 181/181 pass, repo coverage 88.58%, module coverage 90.80%. |
| MCP PoshQC vs fallback | PARTIAL | Evidence artifacts note that `mcp__drmCopilotExtension__run_poshqc_format`, `..._analyze`, `..._test` were not reachable from the executor sandbox. Fallback to direct `Invoke-Formatter` / `Invoke-ScriptAnalyzer` / `Invoke-Pester` 5.6.1 was used. The rule wording says "Use the MCP server functions; do not substitute VS Code task wrappers." The fallback is direct PowerShell primitive invocation, which PoshQC itself wraps, not a VS Code task wrapper. The evidence artifacts disclose the fallback explicitly. Verdict: PARTIAL — intent preserved, mechanism differs. Remediation is not required under the skill contract because the substituted tool is the same primitive PoshQC wraps and the executor recorded the substitution explicitly. |
| Pester v5.x | PASS | Evidence artifacts record Pester 5.6.1. |
| Tests mirror code structure | PASS | `tests/scripts/Invoke-OpenClawContainerPathValidation.*.Tests.ps1` mirrors `scripts/Invoke-OpenClawContainerPathValidation.ps1`. |
| One behavior per `It` | PASS | All retained `It` blocks assert a single behavior. |
| Real code paths over mocks | PASS | Pure helpers (`Get-OpenClawEndpointUri`, `Get-OpenClawEnvFileMap`) are exercised via the real code path; only I/O surfaces (`Invoke-WebRequest`, `docker`) are mocked. |
| No external dependencies | PASS | All network/process calls mocked. |
| Coverage >= 80% / changed >= 90% (repeat) | PASS | Already verified above. |

Domain verdict: PASS (with PARTIAL on MCP toolchain surface noted and authorized by the skill contract).

## Coverage Verification

Per the feature-review-workflow skill, coverage must be verified from the canonical pre-existing coverage artifacts. The skill specifies the canonical PowerShell artifact path as `artifacts/pester/powershell-coverage.xml` (JaCoCo XML). Every language with changed files in the branch diff receives an explicit PASS or FAIL verdict below, computed from the canonical artifact. No scope narrowing is applied.

### Canonical-artifact state (authoritative for this audit)

The canonical artifact `artifacts/pester/powershell-coverage.xml` is present on disk but stale. Its totals and scope are:

- Scope: 5 source files under `.claude/hooks/` only (the five `.ps1` hook scripts). It does not include any file under `scripts/`, `scripts/powershell/modules/`, or `tests/`.
- Totals at the report root element:
  - `counter type="LINE" missed="256" covered="0"`
  - `counter type="INSTRUCTION" missed="362" covered="0"`
  - `counter type="METHOD" missed="13" covered="0"`
  - `counter type="CLASS" missed="5" covered="0"`
- Computed line coverage: 0 / 256 = 0.00%.
- Report header timestamp: `Pester (04/21/2026 08:24:04)`, i.e. prior to this feature's Phase 6 QA run (which ran at `2026-04-21T15:09:00Z`).

The canonical artifact is the authoritative input the feature-review-workflow hook consults to verify coverage. Its totals do not reflect the feature's actual Phase 6 Pester run; it was produced by an earlier, narrower hooks-only Pester invocation that exercised no production code and therefore reported 0% covered lines.

### Per-language verdicts (from canonical artifact)

| Language | Changed files on branch | Canonical artifact | Verdict |
|---|---|---|---|
| PowerShell | 8 (3 production + 5 test, one deletion) | `artifacts/pester/powershell-coverage.xml` | FAIL |
| TypeScript | 0 | `coverage/lcov.info` | PASS (vacuously — no changed files on branch) |
| Python | 0 | `artifacts/python/lcov.info` | PASS (vacuously — no changed files on branch) |
| C# | 0 | `artifacts/csharp/coverage.xml` | PASS (vacuously — no changed files on branch) |

### PowerShell coverage — explicit threshold checks (from canonical artifact)

| Gate | Threshold | Measured (from `artifacts/pester/powershell-coverage.xml`) | Verdict |
|---|---|---|---|
| Repo-wide line coverage | >= 80% | 0.00% (0 / 256 lines covered) | FAIL |
| Changed-file coverage on `scripts/Invoke-OpenClawContainerPathValidation.ps1` | >= 80%, no baseline regression | not present in canonical artifact (scope excludes `scripts/`) | FAIL |
| Changed-file coverage on `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | >= 90% changed-module threshold, no baseline regression | not present in canonical artifact (scope excludes `scripts/powershell/modules/`) | FAIL |
| Changed-file coverage on `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` | >= 80%, no baseline regression | not present in canonical artifact (scope excludes `scripts/powershell/modules/`) | FAIL |

### Remediation trigger

PowerShell coverage FAILs (repo-wide and per-changed-file) are added to remediation triggers. A `remediation-inputs.2026-04-21T15-30.md` artifact is produced alongside this audit. The remediation action is to refresh the canonical `artifacts/pester/powershell-coverage.xml` so its scope covers `scripts/` and `scripts/powershell/modules/` for the current branch, then re-run coverage verification.

### Feature-local coverage measurement (context only — not authoritative)

For review context only, the feature's own Phase 6 coverage pipeline wrote:

- `TestResults/coverage-post.xml` — post-change JaCoCo, 1287 / 1453 commands executed → 88.58% repo-wide, with `OpenClawContainerValidation.psm1` at 90.80%.
- `TestResults/coverage-baseline.xml` — baseline JaCoCo captured via `git stash push -u` replay, 1322 / 1485 commands → 89.02% repo-wide, with the module at 93.78%.
- Consolidated in `evidence/qa-gates/coverage-delta.2026-04-21T14-00.md`.

These figures indicate the branch does not regress coverage in practice and the 80% / 90% thresholds are met when the Pester scope includes the `scripts/` tree. However, the feature-review-workflow contract specifies the canonical artifact path as the verification source, and the canonical artifact reports 0.00%. The audit verdict must therefore record FAIL and trigger remediation even though the feature-local evidence suggests PASS. The canonical-artifact refresh is the corrective action, not a change to the code under review.

### New vs modified classification (PowerShell)

None of the changed PowerShell files are newly added in this branch. `git diff 2397e6d0 -- .` shows the three production PowerShell paths and the five test PowerShell paths already existed at the merge-base SHA, with one test file being a deletion and all others being in-place modifications. This is a factual observation from the branch diff, not a scope narrowing. It does not change the FAIL verdict above because the canonical artifact does not include any of the changed files in its scope.

## Rejected Scope Narrowing

None. The caller prompt supplied the branch diff and feature folder as the authoritative scope and did not attempt to narrow coverage, limit files, or mark any language N/A that has changed files in the branch diff. The coverage verdicts above are explicit PASS/FAIL for every language with changed files.

## Observations outside the bug scope

- `docs/mailbridge-runbook.md` diff includes two additions unrelated to AC-8 DashboardAuth removal:
  1. A new `### 5. Temporarily stop or restart the bridge` section with `schtasks /end`, `Disable-ScheduledTask`, `Enable-ScheduledTask`, and `schtasks /run` guidance, plus the renumbering of the previous `### 5. Remove the scheduled-task deployment` to `### 6.`.
  2. A new `docker compose stop` / `docker compose start` guidance block appended to the assistant-service section.
  These additions are documentation-only, professional in tone, and do not contradict any policy. They are outside the scope of the eight ACs in `issue.md` but do not violate the plan because the plan's Phase 5 authorized edits to `docs/mailbridge-runbook.md` in scope of the feature. They should be noted in the feature audit as "scope leakage" (benign documentation additions not covered by an AC) rather than policy violations.

## Overall Policy Compliance

FAIL — coverage verification fails against the canonical `artifacts/pester/powershell-coverage.xml` artifact required by the feature-review-workflow skill. All non-coverage policy domains verify (code-change, unit-test, tonality, powershell.md toolchain order). The specific failure set:

- Coverage (PowerShell): FAIL on repo-wide line-coverage gate (0.00% in canonical artifact vs 80% floor). FAIL on all three changed-file gates (scope of canonical artifact excludes all three changed production paths).

Two PARTIAL notes, not remediation-triggering on their own, are also documented above:

1. Public API compatibility — intentional breaking change to the validator script's `-DashboardAuthPath` parameter and the result object's `DashboardAuth` property, authorized by the operator-approved issue scope and propagated to the runbook.
2. MCP PoshQC surface — authorized fallback to direct `Invoke-Formatter` / `Invoke-ScriptAnalyzer` / `Invoke-Pester 5.6.1` primitives because the MCP tool surface was unreachable from the executor sandbox. Evidence artifacts disclose the fallback.

Remediation is required. See `remediation-inputs.2026-04-21T15-30.md` in this feature folder for the explicit remediation items and the corrective-action sequence. The underlying code changes on the branch are not at fault; the failure is driven by a stale canonical coverage artifact (scoped to `.claude/hooks/*.ps1` from a 2026-04-21T08:24 run, with 0 covered lines) that predates this feature's Phase 6 Pester run at 2026-04-21T15:09. Refreshing the canonical artifact with a repo-wide Pester coverage run is the remediation action.
