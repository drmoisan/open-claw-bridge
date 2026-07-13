# Policy Compliance Audit — admin-access-automation (Issue #148)

- Timestamp: 2026-07-12T23-57
- Feature branch: `feature/admin-access-automation-148`
- Base branch: `origin/epic/openclaw-runtime-remediation-integration` (merge-base `f35ee45`)
- Work mode: full-feature (from `issue.md` marker `- Work Mode: full-feature`)
- Language in scope: PowerShell 7+ (only language with changed source files)
- Scope: full branch diff vs base (`git diff origin/epic/openclaw-runtime-remediation-integration...HEAD`)

## Scope Confirmation

Changed production/source files on the branch:
- `scripts/Get-OpenClawControlUiTokenUrl.ps1` (new)
- `scripts/Invoke-OpenClawDeviceTokenRotation.ps1` (new)
- `scripts/Set-OpenClawWebSearchProvider.ps1` (new)
- `tests/scripts/Get-OpenClawControlUiTokenUrl.Tests.ps1` (new)
- `tests/scripts/Invoke-OpenClawDeviceTokenRotation.Tests.ps1` (new)
- `tests/scripts/Set-OpenClawWebSearchProvider.Tests.ps1` (new)
- `deploy/docker/openclaw-assistant/openclaw.json` (seed edit, not a coverage-denominator file)
- `docs/mailbridge-runbook.md` (Markdown, exempt from code gates)
- feature docs + evidence (documentation)

No caller attempt to narrow scope was detected. No `## Rejected Scope Narrowing` entries are required.

## Rejected Scope Narrowing

None. The caller prompt requested the full feature-vs-base audit and supplied no plan/task/phase subset that narrows language coverage.

## Evidence Location Compliance

- Branch-diff scan for files under `artifacts/baselines/`, `artifacts/qa/`, `artifacts/evidence/`, `artifacts/coverage/`: none found (0 matches).
- All feature evidence is under the canonical `docs/features/active/2026-07-11-admin-access-automation-148/evidence/<kind>/` path (baseline, qa-gates, regression-testing, other).
- `scripts/dev_tools/validate_evidence_locations.py` is not present in this worktree; a direct branch-diff scan (`git diff --name-only ... | grep '^artifacts/(baselines|qa|evidence|coverage)/'`) returned zero matches, so no location violation exists.
- Verdict: PASS. No `EVIDENCE_LOCATION_OVERRIDE_REJECTED` events; no non-canonical evidence paths.

## Policy Verdicts

### Security — token/secret handling (`.claude/rules/general-code-change.md` Error Handling; spec NFR-S1..S4)

| Rule | Verdict | Evidence |
|---|---|---|
| No plaintext secret in output/verbose/debug/log streams | PASS | `Get-OpenClawControlUiTokenUrl.ps1` writes no token to any stream; the only returned object is the URL (delivery artifact, token in fragment by design per FR-1.5). `Invoke-OpenClawDeviceTokenRotation.ps1` writes the secret only via `Set-Content`; `Write-Verbose` messages reference paths/container names, never the secret. Both test files assert the secret does not appear in Verbose/Debug/Warning/Information/Error records (`*>&1` stream merge). |
| No hard-coded tokens/API keys | PASS | No literal token or key in any of the three scripts. Provider key is the SecretRef `${WEB_SEARCH_API_KEY}` (`Set-OpenClawWebSearchProvider.ps1:81,118`); seed `openclaw.json` stores `"apiKey": "${WEB_SEARCH_API_KEY}"`. |
| SecretRef-style env interpolation for provider key | PASS | `openclaw.json` `plugins.entries.firecrawl.config.webSearch.apiKey = "${WEB_SEARCH_API_KEY}"`, mirroring `gateway.auth.token = "${OPENCLAW_GATEWAY_TOKEN}"`. Script fails if the env var is unset (`Set-OpenClawWebSearchProvider.ps1:76-79`). |
| Cryptographic RNG for rotated secret | PASS | `New-OpenClawDeviceToken` uses `System.Security.Cryptography.RandomNumberGenerator` with `try/finally` dispose (`Invoke-OpenClawDeviceTokenRotation.ps1:110-117`); base64url charset asserted in test (shape only, not value). |

### ShouldProcess gating and idempotency (spec FR-2.4/2.5, FR-3.5/3.7, NFR-S4)

| Rule | Verdict | Evidence |
|---|---|---|
| Rotation gated by ShouldProcess (file write + each restart) | PASS | `[CmdletBinding(SupportsShouldProcess = $true)]`; `ShouldProcess` on file write (`:173`) and inside `Restart-OpenClawConsumer` per restart (`:131`). `-WhatIf` test asserts 0 `Set-Content` and 0 docker calls. |
| Rotation idempotent without `-Force` | PASS | Non-empty existing token short-circuits with `return` before write/restart (`:163-167`); idempotency test asserts 0 writes and 0 restarts. |
| Provisioning gated by ShouldProcess | PASS | `SupportsShouldProcess` on `Set-OpenClawWebSearchProvider.ps1`; seed write gated (`:140`); `-WhatIf` test asserts no write. |
| Provisioning idempotent (no duplicate entries) | PASS | Early `return` when existing `apiKey -eq $secretRef` (`:101-104`); on rebuild it removes-then-re-adds the single entry (`:123-126`). Idempotency test asserts one `Set-Content` across two runs and a single `firecrawl` entry. |

### Container-restart seam (spec FR-2.7, AC-6)

- Verdict: PASS. Restarts flow only through `Invoke-OpenClawDockerCommand` (`Invoke-OpenClawDeviceTokenRotation.ps1:135`). Branch-wide grep found no direct `docker` process invocation; the only `docker` string occurrences are the docstring, the `-DockerExecutablePath` default value passed to the seam, and a throw message. Tests mock the seam, never the executable (spec NFR-P2/NFR-T2).

### PowerShell rules (`.claude/rules/powershell.md`, `.claude/rules/general-code-change.md`)

| Rule | Verdict | Evidence |
|---|---|---|
| Advanced functions, `CmdletBinding()`, validated named params | PASS | All three scripts use `[CmdletBinding()]` with `ValidateNotNullOrEmpty`/`ValidateRange`/`ValidatePattern` attributes. |
| Fail-fast, explicit errors, no silent catch-all | PASS | `$ErrorActionPreference = 'Stop'`; each `catch` re-throws with added context (`Invoke-...:177-179`, `Set-...:72-74,134-135`); absent-file and missing-env paths throw runbook-directed errors. |
| File size <= 500 lines | PASS | Largest file is `Invoke-OpenClawDeviceTokenRotation.Tests.ps1` at 260 lines; all six files < 500 (verified `wc -l`). |
| Lint/analyze clean | PASS | `evidence/qa-gates/final-analyze.2026-07-12T23-05.md`: repo-wide PSScriptAnalyzer via PoshQC = 0 issues. One `SuppressMessageAttribute` on the pure `New-OpenClawDeviceToken` (PSUseShouldProcessForStateChangingFunctions) is justified (RNG-only, no side effects). |
| Format clean | PASS | `evidence/qa-gates/final-format.2026-07-12T23-05.md`: 0 changes. |

### Unit-test policy (`.claude/rules/general-unit-test.md`)

| Rule | Verdict | Evidence |
|---|---|---|
| Test layout mirrors production under `tests/scripts/` | PASS | Three `*.Tests.ps1` files under `tests/scripts/`, mirroring `scripts/`. No colocation. |
| Determinism: no temp files, no sleeps, no network | PASS | Tests use in-memory hashtable pseudo-files with module/script-scoped `Test-Path`/`Get-Content`/`Set-Content` mocks; no `Start-Sleep`, no network, no temp files. RNG output tested by charset shape only (NFR-D2). |
| Mock wrapper seams, not executables | PASS | `Invoke-OpenClawDockerCommand` mocked with matching named params; docker never invoked. |
| AAA structure, one behavior per `It` | PASS | Each `It` uses Arrange/Act/Assert comments; scenarios cover positive, negative, edge, error, idempotency, WhatIf. |

### Coverage (`.claude/rules/quality-tiers.md`, `.claude/rules/general-unit-test.md`)

Coverage artifact present at canonical `artifacts/pester/powershell-coverage.xml` (171 KB, generated 2026-07-12T23-50); numeric summary recorded in `evidence/qa-gates/final-coverage.2026-07-12T23-05.md` and `coverage-delta.2026-07-12T23-05.md`.

Authoritative repo thresholds (uniform per `quality-tiers.md` and `general-unit-test.md`): line >= 85%, branch >= 75%, no regression on changed lines.

| Scope | Line | Branch (INSTRUCTION proxy) | Verdict |
|---|---|---|---|
| Repo-wide (36 files) | 91.09% (1718/1886) | 90.60% (2178/2404) | PASS |
| `Get-OpenClawControlUiTokenUrl.ps1` (new) | 92.31% (12/13) | 93.75% (15/16) | PASS |
| `Invoke-OpenClawDeviceTokenRotation.ps1` (new) | 96.97% (32/33) | 93.02% (40/43) | PASS |
| `Set-OpenClawWebSearchProvider.ps1` (new) | 87.50% (35/40) | 85.71% (48/56) | PASS |
| No regression on changed lines | baseline 90.83% line -> 91.09%; +0.26 pts | +0.34 pts | PASS |

PowerShell coverage verdict: PASS.

Notes / caveats (non-blocking):
1. The repository has no true branch counter in the Pester JaCoCo XML; INSTRUCTION coverage is used as the branch-coverage proxy per established repo precedent. True branch coverage is therefore not directly measured. Both the line metric and the instruction proxy exceed their thresholds.
2. `Set-OpenClawWebSearchProvider.ps1` line coverage (87.50%) is the closest to the 85% floor. It clears the authoritative uniform threshold. (The reviewer's Coverage Verification "Procedure" text references an 80/90 scheme that conflicts with both the authoritative repo rules and the reviewer instruction's own Thresholds subsection, which state 85/75 uniform and that tier-specific lower thresholds are not used. The authoritative 85/75 uniform threshold was applied.)
3. No production file is excluded from measurement; the uncovered lines are the module-load `Import-Module` wiring guard and a few defensive arms (per `coverage-delta` evidence). No prohibited `exclude` entry. Coverage-exclusion policy: PASS.

### Out-of-scope invariant (spec Scope / AC-13)

- Verdict: PASS. `git diff --stat` for `scripts/Invoke-OpenClawAgentOnboarding.ps1` and `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` returns empty — both byte-unchanged. No new gateway-token-generation path was introduced.

### Workflow / benchmark-baseline rules (`.claude/rules/ci-workflows.md`, `.claude/rules/benchmark-baselines.md`)

- Verdict: PASS (not applicable — no diff). No files under `.github/workflows/**` and no benchmark baseline files were changed on the branch. The `modified-workflow-needs-green-run` rule does not trigger. (The evidence baseline filenames containing "baseline" are QC evidence markdown, not benchmark performance baselines.)

## Policy Audit Summary

- All policy dimensions in scope: PASS.
- No Blocking policy findings.
- Non-blocking observations: branch-coverage-proxy measurement caveat; `Set-OpenClawWebSearchProvider.ps1` near the line-coverage floor; property-based-test-density observation (see feature-audit AC-16 / code-review).
