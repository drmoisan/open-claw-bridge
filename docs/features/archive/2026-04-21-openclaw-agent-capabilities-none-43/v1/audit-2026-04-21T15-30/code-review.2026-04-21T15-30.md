---
Timestamp: 2026-04-21T15-30
Purpose: Code-quality review of the bug/openclaw-agent-capabilities-none branch vs development
Audit scope: full branch diff, merge-base 2397e6d0c5a81ae5c6fd87c5a897b039771c1028
---

# Code Review — bug/openclaw-agent-capabilities-none vs development

- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Merge-base SHA: `2397e6d0c5a81ae5c6fd87c5a897b039771c1028`
- Changed-file surface: 8 PowerShell files (3 production + 5 test, one of which is a deletion), 2 Docker/shell files, 1 documentation file.

## Review Scope

This review inspects every file in the branch diff for code-quality issues beyond pure policy compliance (which is documented separately in `policy-audit.2026-04-21T15-30.md`). The focus is on correctness, readability, design, error handling, test quality, and reviewable hazards.

## 1. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`

### Diff summary

1. Deleted the 59-line `Invoke-OpenClawDashboardAuthProbe` function (and its `.SYNOPSIS` docblock) at the bottom of the module.
2. Removed `'Invoke-OpenClawDashboardAuthProbe'` from the `Export-ModuleMember -Function @(...)` array.
3. Formatter-driven reflow of 8 `} else {` / `} catch {` / `} elseif {` constructs into the `}\n<whitespace>else {` form required by `PSPlaceOpenBrace`.

### Strengths

- The deletion is clean: the function, its docblock, and its export entry are removed consistently. No dangling references.
- The formatter-reflow changes are pure whitespace/newline edits with no semantic impact. They bring the module into conformance with the repo's `PSPlaceOpenBrace` style, which was previously inconsistent.
- The remaining 11 exported functions retain their advanced-function form, parameter validation, and uniform return contract via `Get-OpenClawValidationResult`.

### Observations

- Exported function list consistency: after the edit, the manifest (`.psd1`) and the `.psm1` `Export-ModuleMember` block list the same 11 names in the same order. Good.
- The `.psm1`'s top-of-file docblock still says "the four new probes introduced by issue #38" even though one of those four (`Invoke-OpenClawDashboardAuthProbe`) is now removed. The narrative is now strictly accurate only in a historical sense — "four probes were introduced for issue #38; three remain." This is a minor documentation staleness, not a correctness issue. Consider softening to "new probes introduced for issue #38 and subsequently narrowed; see issue folders for details" at a later pass. Not remediation-required.
- The `[pscustomobject]$Details = @{}` parameter on `Get-OpenClawValidationResult` accepts a hashtable default but types it as `[pscustomobject]`. This is pre-existing code, not in the change scope, but flagged for awareness.

### Issues

None blocking.

## 2. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`

### Diff summary

Removed the `'Invoke-OpenClawDashboardAuthProbe'` entry (and its trailing comma) from `FunctionsToExport`.

### Strengths

- The edit is minimal and mechanical. `Test-ModuleManifest` passes per P1-T3 verification.

### Issues

None.

## 3. `scripts/Invoke-OpenClawContainerPathValidation.ps1`

### Diff summary

1. Removed the `.PARAMETER DashboardAuthPath` documentation block from the comment-based help.
2. Removed the `[string]$DashboardAuthPath = '/auth/verify'` parameter from the `param(...)` block.
3. Removed the `$dashboardAuth = Invoke-OpenClawDashboardAuthProbe -AgentBaseUrl ... -AuthPath $DashboardAuthPath` call site.
4. Removed `$dashboardAuth` from the `$endpointDiagnostics` array.
5. Removed the `DashboardAuth = $dashboardAuth` line from the result `[pscustomobject]`.
6. Updated the `.DESCRIPTION` phrase "readiness (/readyz) and dashboard auth on openclaw-agent" to "readiness (/readyz) on openclaw-agent".
7. Formatter-driven reflow of 2 `} else {` into `}\n<ws>else {`.

### Strengths

- Removal is atomic across parameter, docblock, call site, aggregation array, and output object. There is no orphan reference to `$DashboardAuthPath` or `$dashboardAuth`.
- The `$endpointDiagnostics` count is now 6, consistent with the `validator-expected.2026-04-21T14-00.md` evidence and with the Pester assertion `@($result.EndpointDiagnostics).Count | Should -Be 6`.
- The `.SYNOPSIS` block continues to reflect the script's single-responsibility contract.

### Observations

- Line 15 of the comment-based help still says "the four new probes introduced by issue #38" — same staleness as the module docblock. Minor, not remediation-required.
- The script still imports the module with the guarded `if (-not (Get-Module -Name OpenClawContainerValidation)) { Import-Module -Name $moduleManifest -ErrorAction Stop }` pattern. Good — preserves Pester `Mock -ModuleName` semantics.
- `[ValidateRange(1, 300)]` on `-TimeoutSeconds` is preserved.
- The end-of-script branching on `AsJson` / `PassThru` / default output is unchanged.

### Issues

None blocking.

## 4. `deploy/docker/openclaw-agent.Dockerfile`

### Diff summary

Added three blocks between the `FROM` instruction and the first `COPY --chown=1654:1654` instruction:

```dockerfile
# Pre-install the ACP runtime so the embedded agent does not need runtime `npx`
# fetches or writable npm cache (container is read_only with noexec tmpfs).
USER root
RUN npm install -g @zed-industries/codex-acp@0.11.1 \
    && npm cache clean --force \
    && rm -rf /root/.npm /tmp/.npm /tmp/npm-* 2>/dev/null || true
USER 1654

# codex-acp (which wraps the Rust codex CLI) refuses to store its config under
# /tmp and cannot write to the read-only root FS. Point it at a path inside the
# existing /workspace named volume; the entrypoint creates the directory.
ENV CODEX_HOME=/workspace/.codex

# The upstream gateway spawns the ACP backend with `npx @zed-industries/codex-acp@^0.11.1`
# which always consults an npm cache even when the package is globally installed.
# Default cache is $HOME/.npm; HOME is `/` for user 1654 and the root FS is read_only,
# so npm fails with ENOENT on /.npm. Redirect the cache to the /workspace volume.
ENV NPM_CONFIG_CACHE=/workspace/.npm-cache
```

### Strengths

- Pinned package version (`@0.11.1`, not `^0.11.1` or `latest`) — image builds are deterministic across rebuilds unless the Dockerfile is edited.
- `npm cache clean --force` and the subsequent `rm -rf` reduce the final image layer size; the caller explicitly left `|| true` so a missing cache directory on some base-image variants does not fail the build.
- `USER root` → `USER 1654` toggle is scoped tightly around the install step. The final `USER 1654` at line 12 restores the non-root runtime identity before any subsequent `COPY` or `ENTRYPOINT` runs.
- The two `ENV` lines are accompanied by multi-line comments documenting the specific upstream behavior that motivates them (codex-acp config storage refusal and `npx`-always-consults-cache). A reviewer revisiting this file six months later will not have to re-derive the root cause.
- The `ENTRYPOINT` and `CMD` remain byte-identical vs. the merge-base; hardening at the compose layer is not touched.

### Observations

- The pinned `0.11.1` in the Dockerfile and the `^0.11.1` in the upstream gateway's `npx` invocation produce the same resolved version today. If the gateway ever publishes a new-minor/new-major requirement, the Dockerfile pin will need to be re-bumped in lockstep. This risk is documented in `issue.md` AC-4 note: "the upstream gateway still invokes `npx @zed-industries/codex-acp@^0.11.1`; the writable `NPM_CONFIG_CACHE` ... makes that invocation succeed without registry access after the first cached run." Consider adding a follow-up note in the runbook that ties the two version strings together. Not remediation-required.
- The `rm -rf /root/.npm /tmp/.npm /tmp/npm-* 2>/dev/null || true` at the end of the `RUN` is cautious and safe. The `2>/dev/null` suppresses "no such file" output; `|| true` guarantees the layer succeeds.
- `USER 1654` (numeric UID) matches the upstream image convention documented in the existing `COPY --chown=1654:1654` lines below. Using the numeric form instead of a username avoids dependency on an `/etc/passwd` entry present in the base image.
- The two `ENV` declarations are metadata-only and add no runtime layers, which is efficient.

### Issues

None blocking. A potential future consideration: if the upstream gateway switches away from `npx` and resolves `codex-acp` from `PATH`, the `NPM_CONFIG_CACHE` plumbing becomes dead code. That is beyond the scope of this bug fix.

## 5. `deploy/docker/openclaw-agent-entrypoint.sh`

### Diff summary

Added two `mkdir -p` lines after the existing `mkdir -p "$runtime_dir"`:

```sh
mkdir -p "${CODEX_HOME:-$workspace_dir/.codex}"
mkdir -p "${NPM_CONFIG_CACHE:-$workspace_dir/.npm-cache}"
```

### Strengths

- `${VAR:-fallback}` parameter-expansion form matches existing idioms in the script and survives the case where either env var is unset.
- `mkdir -p` is idempotent; re-running the entrypoint on container restart does not cause an error.
- Order of operations: `mkdir -p "$workspace_dir"` and `mkdir -p "$runtime_dir"` already exist immediately above, so the named-volume target (`/workspace`) is guaranteed to exist before the sub-directories are created.
- The script is still `set -eu`, so any failure in the new `mkdir` commands would abort the entrypoint and surface in `docker compose logs`.

### Observations

- No error handling needed beyond `set -eu`; `mkdir -p` on a writable `/workspace` named volume is essentially always going to succeed.
- The fallback paths (`$workspace_dir/.codex` and `$workspace_dir/.npm-cache`) match the canonical values set in the Dockerfile, so "env var unset" produces the same behavior as "env var set to default" — a helpful invariant for anyone debugging from a stripped-down shell.

### Issues

None.

## 6. `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`

### Diff summary

- 4 occurrences of the `'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }` mock branch removed from `Invoke-WebRequest` mocks.
- 1 occurrence of the same branch removed from an `elseif ([string]$Uri -like '*/auth/verify')` clause.
- `@($result.EndpointDiagnostics).Count | Should -Be 7` changed to `Should -Be 6`.
- 2 occurrences of `Should -Be 15` changed to `Should -Be 14` (SupportingDiagnostics count).
- 1 `Should -BeGreaterOrEqual 6` changed to `Should -BeGreaterOrEqual 5` (failing-probe count).
- 1 `$result.DashboardAuth.IsExpected | Should -BeTrue` assertion removed.
- 1 comment updated from "Now 6 endpoint-backed probes will fail: Live, Ready, CoreStatus, AgentDashboard, AgentReadyz, DashboardAuth." to "Now 5 endpoint-backed probes will fail: Live, Ready, CoreStatus, AgentDashboard, AgentReadyz."

### Strengths

- Count arithmetic is consistent: 7 → 6 (endpoint diagnostics), 15 → 14 (supporting diagnostics, which is `1 docker + 6 container + 1 hostadapter + 6 endpoint = 14`), 6 → 5 (failing probes). The numbers add up.
- The removed mock-branches are the only behavioral state that `/auth/verify` held in the test suite. No stub expectations are orphaned.
- The comment narrative is updated in lockstep with the count, preserving review-time clarity.

### Observations

- The test's `BeforeAll` still imports the module via the fixtures helper `Import-OpenClawContainerValidationModule -TestsRoot $PSScriptRoot`, which is unchanged.
- The `Describe 'Invoke-OpenClawContainerPathValidation.ps1'` block assertions on `$result.DockerEngine.IsExpected`, `$result.Live.IsExpected`, `$result.HostAdapterInContainer.IsExpected`, etc. are intact. The removal is limited to DashboardAuth surface.
- The test's failure-count lower bound (`Should -BeGreaterOrEqual 5`) uses `-GreaterOrEqual` rather than `-Be 5`. This is deliberate — the comment explains that `GatewayTokenPresence` is still expected because `Get-Content` is mocked to return a token — and is consistent across pre- and post-edit behavior.

### Issues

None.

## 7. `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1`

### Diff summary

Removed 3 occurrences of the `'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }` mock branch.

### Strengths

- Purely mechanical; no assertions touched. The `/auth/verify` URIs were purely there to prevent the mock's `default { '{}' }` branch from hitting unexpected URIs; with the production probe removed, no request is ever made.

### Issues

None.

## 8. `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1`

### Diff summary

Removed 3 occurrences of the `'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }` mock branch.

### Strengths

- Same as above: purely mechanical removal.

### Issues

None.

## 9. `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1`

### Diff summary

Removed 1 occurrence of the `'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }` mock branch.

### Strengths

- Same pattern — purely mechanical removal.

### Issues

None.

## 10. `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1`

### Diff summary

Full file deletion (177 lines). The file contained four `It` blocks, all tagged `ExpectFail-Phase2` or `ExpectFail-Phase5`, that exercised the (now-removed) `DashboardAuth` probe surface.

### Strengths

- Deleting the test file is the correct response to deleting the production surface. Leaving these tests in place would produce false negatives (failing tests for a deliberately removed behavior).
- The file deletion reduces the test suite by exactly 5 tests; the baseline-to-post coverage delta (186 → 181) matches.

### Observations

- Before deletion, the file's `BeforeAll` imported the fixtures helper and the module manifest. No state was left behind because those imports are scoped to the `BeforeAll` block and `AfterEach` cleaned up `$script:` variables.

### Issues

None. No orphan fixtures imports or leftover helpers.

## 11. `docs/mailbridge-runbook.md`

### Diff summary

1. Removed the `- DashboardAuth is expected when a POST to the dashboard auth endpoint with the stored token returns HTTP 200 and a JSON body.` bullet from the "Expected behavior" section (on or near line 472).
2. Removed the entire `#### Validation-script dashboard-auth overrides` subsection (heading + descriptive paragraph) that previously documented the `-DashboardAuthPath` parameter.
3. Added a new `### 5. Temporarily stop or restart the bridge` section with `schtasks /end`, `Disable-ScheduledTask`, `Enable-ScheduledTask`, and `schtasks /run` guidance; renumbered the former "5. Remove the scheduled-task deployment" section to `### 6.`.
4. Added a new `docker compose stop` / `docker compose start` guidance block in the assistant-service section.

### Strengths

- The two DashboardAuth-related removals correctly eliminate dangling references per AC-8 and AC-3.
- The new bridge stop/restart and docker stop/start guidance blocks use idiomatic PowerShell examples, match the existing runbook style, and are factually accurate for Windows Task Scheduler + Docker Compose behavior.
- All new code blocks are syntactically valid PowerShell; none introduces ambiguity about which shell to run them in.

### Observations

- The two new runbook additions (3 and 4 above) are outside the scope of the eight ACs declared in `issue.md` and outside the scope enumerated in `plan.md` Phase 5 (which authorized only the DashboardAuth removals). They are benign documentation additions and pass all policy checks, but a strict reviewer may call this out as "scope leakage." In practice, extending operator guidance while the runbook is already being edited is common and low-risk; the additions do not contradict any existing behavior. Flagged for visibility in the feature audit, not remediation-required.
- Section renumbering 5 → 6 preserves the cross-reference style used elsewhere in the runbook.

### Issues

None blocking.

## Cross-Cutting Themes

### Error-handling consistency

The probe removal deletes the only production surface that returned early with a synthetic failure result when a precondition was missing (no gateway token). Every remaining probe either returns a real network result or a real docker-CLI result. The error-handling surface of the validator is therefore more uniform post-change than it was pre-change.

### Test maintainability

The test suite contains multiple duplicated `Invoke-WebRequest` mock implementations (one per `It` block). Each block's switch statement independently enumerates the URIs the validator probes. This pattern is redundant but pre-existing; the DashboardAuth removal did not introduce new duplication, it only reduced existing duplication by one branch per block. A future-pass refactor could extract a shared default-URI-map fixture in `tests/scripts/fixtures/`, but that is out of scope for this bug fix.

### Image-layer hygiene

The Dockerfile addition increases image size by one npm layer but explicitly removes the cache directory in the same `RUN`. This is the right pattern — combining install and clean in a single `RUN` prevents the intermediate cache from being preserved in the image history.

### Security hardening

All six hardening tokens (`cap_drop`, `read_only: true`, `no-new-privileges`, `noexec`, `nosuid`, `nodev`) are preserved in `docker-compose.yml` per evidence `compose-hardening.2026-04-21T14-00.md`. The Dockerfile change does not weaken any compose-level constraint; it only shifts writable state onto the pre-existing `/workspace` named volume, which was already writable.

## Overall Code-Quality Verdict

PASS. No blocking issues. Minor observations on documentation staleness and scope-leaking runbook additions are documented above for visibility but do not require remediation.
