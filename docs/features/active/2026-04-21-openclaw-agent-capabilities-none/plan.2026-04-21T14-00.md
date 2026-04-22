# Plan — OpenClaw agent `capabilities=none` + DashboardAuth probe removal

- Feature folder: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/`
- Branch: `bug/openclaw-agent-capabilities-none` (base: `development`)
- Plan target (update-in-place): `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/plan.2026-04-21T14-00.md`
- Work Mode: full-bug
- Plan timestamp: 2026-04-21T14-00

## Scope Summary

This plan delivers exactly two operator-approved fixes from `issue.md`:

1. Fix Option 1B — remove the `DashboardAuth` probe surface entirely from the validator (script, module, manifest, tests, runbook).
2. Fix Option 2A — pre-install `@zed-industries/codex-acp@0.11.1` globally in the `openclaw-agent` image so the embedded ACP runtime starts without runtime `npx` fetches.

No changes to `docker-compose.yml`, archived audit artifacts under `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/`, the HostAdapterInContainer probe, the upstream openclaw image, or unrelated active features.

## Acceptance Criteria Coverage Map

| AC | Task that closes it |
| --- | --- |
| AC-1 — agent starts with tool-capable plugin runtime, no `embedded acpx runtime backend probe failed` in logs | `[P5-T3]` + `[P5-T4]` (log-grep check and gateway plugin count check after `docker compose up --force-recreate`) |
| AC-2 — validator returns `OverallResult: Expected` on a healthy stack | `[P2-T1]` (endpoint array + result pscustomobject no longer reference `DashboardAuth`) and `[P6-T4]` (Pester re-run against mocks that no longer include `/auth/verify` confirms `Expected`) |
| AC-3 — `DashboardAuth` probe removed from validator surface (function, call site, script parameter, result field, tests) | `[P2-T1]`, `[P2-T2]`, `[P2-T3]`, `[P3-T1]`, `[P3-T2]`, `[P3-T3]`, `[P6-T5]` (post-change repo-wide Grep returns zero matches outside archived folder) |
| AC-4 — agent image embeds `@zed-industries/codex-acp@0.11.1` at `/usr/local/lib/node_modules/@zed-industries/codex-acp/package.json` | `[P4-T1]` (Dockerfile RUN layer) + `[P5-T5]` (post-recreate exec verification of the package.json path) |
| AC-5 — existing hardening (`read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, tmpfs `noexec`/`nosuid`/`nodev`) preserved | `[P4-T2]` (verify `docker-compose.yml` unchanged via `git diff --exit-code`) |
| AC-6 — PoshQC format → analyze → test runs clean on changed files | `[P6-T1]`, `[P6-T2]`, `[P6-T3]`, `[P6-T4]` (full QA loop) |
| AC-7 — repo-wide Pester line coverage ≥ 80%, changed module ≥ 90% | `[P6-T4]` (coverage-enabled Pester) + `[P6-T6]` (coverage delta/threshold verification vs Phase 0 baseline) |
| AC-8 — runbook updated where it mentioned `DashboardAuth` | `[P5-T1]`, `[P5-T2]` |

---

### Phase 0 — Baseline capture and policy read

- [x] [P0-T1] Read `.claude/rules/general-code-change.md` and `.claude/rules/general-unit-test.md` and `.claude/rules/tonality.md` and `.claude/rules/powershell.md` in that order. Acceptance: write a policy-read evidence artifact at `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/phase0-instructions-read.2026-04-21T14-00.md` containing `Timestamp:`, `Policy Order:`, and the explicit list of the four files read. Verification: `Get-Item 'docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/phase0-instructions-read.2026-04-21T14-00.md'` succeeds.

- [x] [P0-T2] Capture current git HEAD SHA for the working branch. Run `git rev-parse HEAD` and `git status --porcelain` from repo root. Acceptance: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/baseline/git-head.2026-04-21T14-00.md` containing `Timestamp:`, `Command: git rev-parse HEAD`, `EXIT_CODE:`, and `Output Summary:` with the SHA and a one-line summary of `git status --porcelain`. Verification: file exists and records a non-empty 40-char SHA.

- [x] [P0-T3] Run PoshQC format in read-only/check mode against the repo to record the current formatter state. MCP command: `mcp__drmCopilotExtension__run_poshqc_format`. Acceptance: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/baseline/poshqc-format.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and an `Output Summary:` stating whether the run made any in-memory changes and listing any files that would be reformatted. Verification: file exists and `EXIT_CODE:` is present.

- [x] [P0-T4] Run PoshQC analyzer for a baseline PSScriptAnalyzer snapshot. MCP command: `mcp__drmCopilotExtension__run_poshqc_analyze`. Acceptance: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/baseline/poshqc-analyze.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (count of errors/warnings/info and top-level rule IDs). Verification: file exists and `EXIT_CODE:` is recorded.

- [x] [P0-T5] Run PoshQC Pester with coverage enabled using `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`. MCP command: `mcp__drmCopilotExtension__run_poshqc_test`. Acceptance: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/baseline/poshqc-test.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing numeric pass/fail counts, repo-wide line-coverage percentage, and the `OpenClawContainerValidation` module coverage percentage as the baseline for AC-7. Verification: file exists and records both the repo-wide and module-scoped coverage numbers.

- [x] [P0-T6] Capture current Docker build+run baseline for the agent image (pre-fix). Commands (Bash): `docker compose config openclaw-agent > /tmp/compose-snapshot.txt` and `docker compose logs --tail=200 openclaw-agent 2>&1 | tee /tmp/agent-logs.txt` (only if the container is currently running — if not, record `not-running`). Acceptance: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/baseline/agent-container-state.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` containing the exact `[plugins] embedded acpx runtime backend probe failed` line from the captured logs (or `not running` if the container was not up). Verification: file exists and either includes the failure line or `not running`.

---

### Phase 1 — PowerShell production edits (validator module)

Production files in scope for this phase (≤ 3 production edits, 0 test edits):

1. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`
2. `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`

- [x] [P1-T1] In `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, delete the entire `Invoke-OpenClawDashboardAuthProbe` function definition (current lines 338–396, including the `<# .SYNOPSIS #>` docblock immediately above it and the trailing blank line between it and the `Export-ModuleMember` block). Acceptance: `Invoke-OpenClawDashboardAuthProbe` is no longer defined anywhere in the file. Verification: `Grep -n "Invoke-OpenClawDashboardAuthProbe" scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` returns zero matches.

- [x] [P1-T2] In the same file `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1`, remove the `'Invoke-OpenClawDashboardAuthProbe'` entry from the `Export-ModuleMember -Function @(...)` list at the bottom of the file (current line 410) and adjust the preceding comma on line 409 so the list remains syntactically valid. Acceptance: the `Export-ModuleMember` array no longer names `Invoke-OpenClawDashboardAuthProbe`. Verification: `Grep -n "Invoke-OpenClawDashboardAuthProbe" scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` returns zero matches, AND `pwsh -NoProfile -Command "Import-Module ./scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1 -Force; Get-Command -Module OpenClawContainerValidation | Select-Object -ExpandProperty Name"` does not list `Invoke-OpenClawDashboardAuthProbe`.

- [x] [P1-T3] In `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1`, remove the `'Invoke-OpenClawDashboardAuthProbe'` entry from the `FunctionsToExport` array (current line 22) and remove the trailing comma on line 21 so the array ends cleanly. Acceptance: the manifest no longer exports `Invoke-OpenClawDashboardAuthProbe`. Verification: `Grep -n "Invoke-OpenClawDashboardAuthProbe" scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` returns zero matches, AND `pwsh -NoProfile -Command "Test-ModuleManifest ./scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1 | Out-Null; $LASTEXITCODE"` exits 0.

---

### Phase 2 — PowerShell production edits (validator script)

Production files in scope for this phase (1 production edit, 0 test edits):

1. `scripts/Invoke-OpenClawContainerPathValidation.ps1`

- [x] [P2-T1] In `scripts/Invoke-OpenClawContainerPathValidation.ps1`, remove the `[string]$DashboardAuthPath = '/auth/verify',` parameter line from the `param(...)` block (current line 39) and remove the corresponding `.PARAMETER DashboardAuthPath` docblock section (current lines 18–21, the three-line `.PARAMETER` entry plus the relative path description). Acceptance: the script no longer declares `DashboardAuthPath` as a parameter or documents it. Verification: `Grep -n "DashboardAuthPath" scripts/Invoke-OpenClawContainerPathValidation.ps1` returns zero matches.

- [x] [P2-T2] In the same script, delete the line that invokes the probe: `$dashboardAuth = Invoke-OpenClawDashboardAuthProbe -AgentBaseUrl $AgentBaseUrl -TimeoutSeconds $TimeoutSeconds -EnvFilePath $EnvFilePath -AuthPath $DashboardAuthPath` (current line 257). Acceptance: the `Invoke-OpenClawDashboardAuthProbe` call site is gone. Verification: `Grep -n "Invoke-OpenClawDashboardAuthProbe" scripts/Invoke-OpenClawContainerPathValidation.ps1` returns zero matches.

- [x] [P2-T3] In the same script, remove `$dashboardAuth` from the `$endpointDiagnostics` array (current line 265: change `@($live, $ready, $coreStatus, $agentDashboard, $agentReadyz, $tokenPresence, $dashboardAuth)` to `@($live, $ready, $coreStatus, $agentDashboard, $agentReadyz, $tokenPresence)`), AND remove the `DashboardAuth          = $dashboardAuth` line from the `[pscustomobject]@{...}` result block (current line 287). Update the `.DESCRIPTION` block language at lines 6–11 so the phrase `readiness (/readyz) and dashboard auth on openclaw-agent` becomes `readiness (/readyz) on openclaw-agent` (remove the `and dashboard auth` phrase). Acceptance: the result object has no `DashboardAuth` property and the endpoint diagnostics array has exactly six elements. Verification: `Grep -n "DashboardAuth\|\\$dashboardAuth\|dashboard auth" scripts/Invoke-OpenClawContainerPathValidation.ps1` returns zero matches.

---

### Phase 3 — PowerShell test edits (mocks + deletions)

Test files in scope for this phase (0 production edits, ≤ 3 test edits):

1. `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` (delete)
2. `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`
3. `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1`

- [x] [P3-T1] Delete the entire file `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1`. Acceptance: the file no longer exists on disk. Verification: `Get-Item tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1 -ErrorAction SilentlyContinue` returns nothing, AND `Grep -n "DashboardAuth" tests/scripts/` returns zero matches from that deleted file.

- [x] [P3-T2] In `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`, remove every `/auth/verify` mock branch and every `DashboardAuth` assertion. Specifically:
  - Delete line 51 (`'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }`).
  - Delete line 101 (`$result.DashboardAuth.IsExpected | Should -BeTrue`).
  - On line 93, change `@($result.EndpointDiagnostics).Count | Should -Be 7` to `Should -Be 6`.
  - On line 102, change `@($result.SupportingDiagnostics).Count | Should -Be 15` to `Should -Be 14`.
  - Delete line 130 (`'/auth/verify' { '{"ok":true}' }`).
  - Delete line 189 (`'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }`).
  - Delete line 264 (`'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }`).
  - On line 307, update the comment from `Now 6 endpoint-backed probes will fail: Live, Ready, CoreStatus, AgentDashboard, AgentReadyz, DashboardAuth.` to `Now 5 endpoint-backed probes will fail: Live, Ready, CoreStatus, AgentDashboard, AgentReadyz.`.
  - On line 309, update `Should -BeGreaterOrEqual 6` to `Should -BeGreaterOrEqual 5`.
  - Delete lines 335–337 (the `} elseif ([string]$Uri -like '*/auth/verify') { '{"ok":true}' }` branch), preserving the surrounding `elseif` chain and default.
  - On line 367, change `$result.SupportingDiagnostics.Count | Should -Be 15` to `Should -Be 14`.
  Acceptance: no `DashboardAuth`, `/auth/verify`, or `DashboardAuthPath` references remain in this file. Verification: `Grep -nE "DashboardAuth|/auth/verify|DashboardAuthPath" tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` returns zero matches.

- [x] [P3-T3] In `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1`, delete the three `'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }` mock branches (current lines 38, 92, 139). Acceptance: no `/auth/verify` mock branches remain. Verification: `Grep -n "/auth/verify" tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` returns zero matches.

---

### Phase 3B — Remaining PowerShell test edits

Test files in scope for this phase (0 production edits, ≤ 2 test edits):

1. `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1`
2. `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1`

- [x] [P3B-T1] In `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1`, delete the three `'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }` mock branches (current lines 40, 64, 89). Acceptance: no `/auth/verify` references remain. Verification: `Grep -n "/auth/verify" tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1` returns zero matches.

- [x] [P3B-T2] In `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1`, delete the single `'http://127.0.0.1:18789/auth/verify' { '{"ok":true}' }` mock branch (current line 38). Acceptance: no `/auth/verify` references remain. Verification: `Grep -n "/auth/verify" tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1` returns zero matches.

---

### Phase 4 — Dockerfile edit

Production files in scope for this phase (1 production edit, 0 test edits):

1. `deploy/docker/openclaw-agent.Dockerfile`

- [x] [P4-T1] In `deploy/docker/openclaw-agent.Dockerfile`, insert a new `RUN` instruction between the current line 4 (`FROM ${OPENCLAW_AGENT_IMAGE}`) and the current line 8 (the first `COPY --chown=1654:1654 deploy/docker/openclaw-assistant /opt/openclaw-assistant-seed`). Insert this exact two-line block (preceded by a blank separator line and followed by a blank separator line) before the `# Bake the assistant workspace ...` comment:

  ```dockerfile
  # Pre-install the ACP runtime so the embedded agent does not need runtime `npx`
  # fetches or writable npm cache (container is read_only with noexec tmpfs).
  USER root
  RUN npm install -g @zed-industries/codex-acp@0.11.1 \
      && npm cache clean --force \
      && rm -rf /root/.npm /tmp/.npm /tmp/npm-* 2>/dev/null || true
  USER 1654
  ```

  The exact version string `@zed-industries/codex-acp@0.11.1` must be pinned (not `^0.11.1`, not `latest`). The trailing `rm -rf` ensures no cache is left in the final image layer. The `USER root`/`USER 1654` toggle is required because the upstream image runs as UID `1654` (see existing `COPY --chown=1654:1654` on lines 8–9) and `npm install -g` needs write access to `/usr/local/lib/node_modules`. Acceptance: the Dockerfile contains one new `RUN npm install -g @zed-industries/codex-acp@0.11.1` line followed by `&& npm cache clean --force`; the `FROM`, the two `COPY --chown=1654:1654` lines, the `ENTRYPOINT`, and the `CMD` remain byte-identical to the pre-edit baseline. Verification: `Grep -n "codex-acp@0.11.1" deploy/docker/openclaw-agent.Dockerfile` returns exactly one match, AND `Grep -n "^FROM \${OPENCLAW_AGENT_IMAGE}\|ENTRYPOINT\|^CMD" deploy/docker/openclaw-agent.Dockerfile` returns the same three lines as before the edit.

- [x] [P4-T2] Confirm no other compose or hardening surface was touched in this phase. Run `git diff --name-only` from repo root. Acceptance: `docker-compose.yml` is NOT in the diff list. Verification (Bash): `git diff --name-only origin/development...HEAD | grep -E '^docker-compose\.yml$' && exit 1 || exit 0` — the command must exit 0 (no match). Write the resulting file list to `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/dockerfile-only-diff.2026-04-21T14-00.md` with `Timestamp:`, `Command: git diff --name-only origin/development...HEAD`, `EXIT_CODE:`, and `Output Summary:` listing the changed files.

### Phase 4B — CODEX_HOME plumbing (plan amendment 2026-04-21T14-50)

Amendment rationale: P5-T4 runtime verification revealed that pre-installing `@zed-industries/codex-acp` was necessary but not sufficient. The `codex-acp` wrapper spawns the Rust `codex` CLI, which refuses to place its config under `/tmp` and fails with "Read-only file system (os error 30)" when `$HOME=/` on the `read_only` root FS. The wrapper exits before ACP handshake, so the gateway records `embedded acpx runtime backend probe failed` even though the package is installed. Fix: point `codex` at a writable location inside the existing `/workspace` named volume by setting `CODEX_HOME=/workspace/.codex` in the image and ensuring the entrypoint creates the directory. No change to `docker-compose.yml`; hardening preserved.

Production files in scope for this phase (2 production edits, 0 test edits):

1. `deploy/docker/openclaw-agent.Dockerfile`
2. `deploy/docker/openclaw-agent-entrypoint.sh`

- [x] [P4B-T1] In `deploy/docker/openclaw-agent.Dockerfile`, add `ENV CODEX_HOME=/workspace/.codex` after the `USER 1654` line following the `RUN npm install` block, with a short comment explaining why. Acceptance: `Grep -n "CODEX_HOME" deploy/docker/openclaw-agent.Dockerfile` returns exactly one match.

- [x] [P4B-T2] In `deploy/docker/openclaw-agent-entrypoint.sh`, add `mkdir -p "${CODEX_HOME:-$workspace_dir/.codex}"` after the existing `mkdir -p "$runtime_dir"` line so the directory exists before the gateway probes the ACP backend. Use the `${CODEX_HOME:-fallback}` pattern so the script stays safe if the env var is later unset. Acceptance: `Grep -n "CODEX_HOME" deploy/docker/openclaw-agent-entrypoint.sh` returns exactly one match.

### Phase 4C — npm cache plumbing (plan amendment 2026-04-21T14-55)

Amendment rationale: The upstream gateway spawns the ACP backend using `npx @zed-industries/codex-acp@^0.11.1` regardless of whether the package is globally installed. `npx` always consults the npm package cache to resolve the version range. The default cache path is `$HOME/.npm`; for user `1654` with `HOME=/`, that evaluates to `/.npm` which the read-only root FS rejects (`mkdir /.npm: ENOENT`). Net effect: pre-installing the package is necessary but insufficient — `npx` still fails at the cache-write step before ever running the globally-installed binary. Fix: redirect npm's cache to a writable path under the existing `/workspace` named volume by setting `NPM_CONFIG_CACHE=/workspace/.npm-cache` in the image and pre-creating the directory in the entrypoint. No `docker-compose.yml` change.

Production files in scope for this phase (2 production edits, 0 test edits — the same two files as Phase 4B, minimal additional lines):

1. `deploy/docker/openclaw-agent.Dockerfile` (append one `ENV` line and a short comment)
2. `deploy/docker/openclaw-agent-entrypoint.sh` (append one `mkdir -p` line)

- [x] [P4C-T1] In `deploy/docker/openclaw-agent.Dockerfile`, after the `ENV CODEX_HOME=/workspace/.codex` line add `ENV NPM_CONFIG_CACHE=/workspace/.npm-cache` with an explanatory comment. Acceptance: `Grep -n "NPM_CONFIG_CACHE" deploy/docker/openclaw-agent.Dockerfile` returns exactly one match.

- [x] [P4C-T2] In `deploy/docker/openclaw-agent-entrypoint.sh`, after the `mkdir -p "${CODEX_HOME:-...}"` line add `mkdir -p "${NPM_CONFIG_CACHE:-$workspace_dir/.npm-cache}"`. Acceptance: `Grep -n "NPM_CONFIG_CACHE" deploy/docker/openclaw-agent-entrypoint.sh` returns exactly one match.

---

### Phase 5 — Runbook edits and runtime verification

Production files in scope for this phase (2 production edits — docs only, 0 test edits):

1. `docs/mailbridge-runbook.md` (two edits)

- [x] [P5-T1] In `docs/mailbridge-runbook.md`, delete line 472 (the `- DashboardAuth is expected when a POST to the dashboard auth endpoint with the stored token returns HTTP 200 and a JSON body.` bullet). Acceptance: that bullet no longer appears. Verification: `Grep -n "DashboardAuth" docs/mailbridge-runbook.md` returns zero matches from that line.

- [x] [P5-T2] In `docs/mailbridge-runbook.md`, delete the entire `#### Validation-script dashboard-auth overrides` subsection (the `####` heading on or near line 602 and the full paragraph on line 604 that begins `scripts/Invoke-OpenClawContainerPathValidation.ps1 exposes an optional -DashboardAuthPath parameter`), leaving the surrounding `#### Onboarding parameter overrides` and `### Troubleshooting` sections intact. Acceptance: the subsection heading and paragraph are removed. Verification: `Grep -nE "DashboardAuth|/auth/verify|DashboardAuthPath|dashboard-auth overrides" docs/mailbridge-runbook.md` returns zero matches.

- [x] [P5-T3] Rebuild the agent image using the edited Dockerfile. Commands (Bash, from repo root): `docker compose build openclaw-agent 2>&1 | tee /tmp/agent-build.log`. Acceptance: the build exits 0 and produces a new image tag. Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/docker-build.2026-04-21T14-00.md` with `Timestamp:`, `Command: docker compose build openclaw-agent`, `EXIT_CODE:`, and `Output Summary:` (final image digest/ID and the line count of `/tmp/agent-build.log`).

- [x] [P5-T4] Recreate the agent container and capture startup logs. Commands (Bash, from repo root): `docker compose up -d --force-recreate openclaw-agent`, then wait 15 seconds, then `docker compose logs --tail=300 openclaw-agent 2>&1 | tee /tmp/agent-logs-post.log`. Acceptance: AC-1 is met — the captured log contains `[gateway] ready` AND does NOT contain the substring `embedded acpx runtime backend probe failed`. Also verify the gateway plugin count line shows a plugin list that now includes `codex` (or an ACP-backed entry). Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/docker-recreate-logs.2026-04-21T14-00.md` with `Timestamp:`, `Command: docker compose up -d --force-recreate openclaw-agent && docker compose logs --tail=300 openclaw-agent`, `EXIT_CODE:`, and `Output Summary:` quoting (a) the `[gateway] ready (N plugins: ...)` line verbatim (confirming N has grown vs baseline and the plugin list no longer omits the ACP plugin) and (b) the exact shell command used to confirm absence: `grep -c "embedded acpx runtime backend probe failed" /tmp/agent-logs-post.log` returning `0`.

- [x] [P5-T5] Verify the pre-installed package is resolvable inside the running container. Command (Bash): `docker compose exec -T openclaw-agent sh -c 'ls /usr/local/lib/node_modules/@zed-industries/codex-acp/package.json'`. Acceptance: AC-4 is met — the command exits 0 and prints the package.json path. Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/codex-acp-embedded.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` containing the path `/usr/local/lib/node_modules/@zed-industries/codex-acp/package.json` plus the output of `docker compose exec -T openclaw-agent sh -c 'cat /usr/local/lib/node_modules/@zed-industries/codex-acp/package.json | grep \"version\"'` confirming version `0.11.1`.

---

### Phase 6 — Final QA loop and coverage/regression gate

- [x] [P6-T1] Run PoshQC formatter against the edited PowerShell files. MCP command: `mcp__drmCopilotExtension__run_poshqc_format`. If the step rewrites any file, restart the full toolchain loop from `[P6-T1]` per `.claude/rules/powershell.md`. Acceptance: formatter reports no files changed. Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/final-poshqc-format.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` stating `No files reformatted`.

- [x] [P6-T2] Run PoshQC analyzer. MCP command: `mcp__drmCopilotExtension__run_poshqc_analyze`. Acceptance: analyzer reports zero errors and no new warnings relative to the Phase 0 snapshot in `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/baseline/poshqc-analyze.2026-04-21T14-00.md`. Restart from `[P6-T1]` if analyzer autofixes any files. Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/final-poshqc-analyze.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE:`, and `Output Summary:` with the current error/warning/info count alongside the baseline counts and the delta (must be ≤ 0 for errors and ≤ baseline for warnings).

- [x] [P6-T3] Type-check step is not applicable for PowerShell; record a skip-with-reason artifact. Acceptance: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/final-typecheck.2026-04-21T14-00.md` with `Timestamp:`, `Command: N/A`, `EXIT_CODE: 0`, and `Output Summary: type-check not applicable for PowerShell (see .claude/rules/powershell.md step 3)`. This is an explicitly authorized skip per the language rule.

- [x] [P6-T4] Run PoshQC Pester with coverage enabled using the repo config at `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`. MCP command: `mcp__drmCopilotExtension__run_poshqc_test`. Acceptance: all Pester suites pass with zero regression relative to baseline pass count (`[P0-T5]`); every `*.Tests.ps1` file under `tests/scripts/` that previously referenced `DashboardAuth` / `/auth/verify` now runs clean; repo-wide line coverage ≥ 80% (AC-7) AND coverage on `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` ≥ 90% (AC-7 — the "changed module" threshold, since `Invoke-OpenClawDashboardAuthProbe` was removed from this module). Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/final-poshqc-test.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` containing numeric pass/fail/skip counts, repo-wide line-coverage percentage, and the `OpenClawContainerValidation` module coverage percentage.

- [x] [P6-T5] Execute the AC-3 "zero production-code matches" check. Command (Bash, from repo root): search every path that is NOT under the archived folder `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/` and NOT under `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/` (the active feature folder, which legitimately discusses the removal) for `DashboardAuth`, `Invoke-OpenClawDashboardAuthProbe`, `/auth/verify`, and `DashboardAuthPath`. Use: `rg -n --glob '!docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/**' --glob '!docs/features/active/2026-04-21-openclaw-agent-capabilities-none/**' -e 'DashboardAuth' -e 'Invoke-OpenClawDashboardAuthProbe' -e '/auth/verify' -e 'DashboardAuthPath'`. Acceptance: the command prints nothing and exits non-zero (ripgrep's "no matches" exit). Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/dashboard-auth-grep.2026-04-21T14-00.md` with `Timestamp:`, `Command:` (the full rg command), `EXIT_CODE:` (expect 1 for "no matches"), and `Output Summary: zero matches outside archived audit folders`.

- [x] [P6-T6] Coverage-delta / threshold verification. Using the baseline `[P0-T5]` artifact and the post-change `[P6-T4]` artifact, compute baseline vs post-change repo-wide coverage and module-scoped coverage. Acceptance: post-change repo-wide coverage ≥ max(80, baseline - 0.0) AND module coverage on `OpenClawContainerValidation.psm1` ≥ 90% AND no changed line regressed in coverage. If any threshold is not met, mark remediation-required and do not report PASS. Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/coverage-delta.2026-04-21T14-00.md` with `Timestamp:`, `Command: derived from [P0-T5] and [P6-T4] artifacts`, `EXIT_CODE: 0` only if all thresholds are met, and `Output Summary:` listing `BaselineRepoCoverage:`, `PostChangeRepoCoverage:`, `BaselineModuleCoverage:`, `PostChangeModuleCoverage:`, and `ChangedLinesCoverage:` values.

- [x] [P6-T7] Compose hardening preservation check for AC-5. Command (Bash, from repo root): `git diff origin/development...HEAD -- docker-compose.yml`. Acceptance: the diff is empty (AC-5: `docker-compose.yml` is byte-identical to base). Also grep the current `docker-compose.yml` for the hardening tokens that must remain present: `grep -E 'cap_drop:|read_only: true|no-new-privileges|noexec|nosuid|nodev' docker-compose.yml` must list the same lines as before. Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/compose-hardening.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` either `docker-compose.yml unchanged vs base` OR listing the exact hardening tokens still present.

- [x] [P6-T8] Validator end-to-end sanity against a healthy mock (AC-2). Command (PowerShell): run the full Pester suite `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` filtered to the first `It` block (the `returns expected when all container endpoints match their validation contracts` test). Acceptance: the test passes and `$result.OverallResult` resolves to `Expected` with `EndpointDiagnostics.Count == 6` and no `DashboardAuth` property in the result object. Verification: write `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/qa-gates/validator-expected.2026-04-21T14-00.md` with `Timestamp:`, `Command:`, `EXIT_CODE: 0`, and `Output Summary:` quoting the asserted count and `OverallResult` value. This task closes AC-2.

---

## Preflight Validation

This plan file is finalized at the target path `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/plan.2026-04-21T14-00.md`. The caller should run `validate_orchestration_artifacts` with `artifact_type: "plan"` and `artifact_path` set to this exact path, and should treat preflight revision requests as in-place edits to this same file.

- Plan-path continuity: all revisions in this cycle update this file — no sibling timestamped files will be created.
- Directive line for downstream executor handoff: `DIRECTIVE: PREFLIGHT VALIDATION ONLY`.
- Required signal on return: `PREFLIGHT: ALL CLEAR` or `PREFLIGHT: REVISIONS REQUIRED` with a precise delta.
