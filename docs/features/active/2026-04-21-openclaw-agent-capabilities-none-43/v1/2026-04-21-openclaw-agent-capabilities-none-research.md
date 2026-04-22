# Research: openclaw-agent capabilities=none and validator false-Unexpected

- Researched (UTC): 2026-04-21T00:00:00Z
- Branch: `bug/openclaw-agent-capabilities-none`
- Issue doc: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md`
- Researcher: Task Researcher Agent

---

## 1. Executive Summary

Two independent defects prevent the solution from functioning end-to-end. First, the `openclaw-agent` container self-reports `capabilities=none` and refuses to issue HTTP calls to the HostAdapter because the embedded ACP runtime (`npx @zed-industries/codex-acp@^0.11.1`) cannot download or execute its binary at startup: the container runs as UID 1654, is mounted `read_only: true`, and the only writable tmpfs mounts (`/tmp`, `/.openclaw`) carry `noexec,nosuid,nodev` flags, so `npx` fails at the cache-directory `mkdir` step before any binary executes. Second, the container-path validator (`scripts/Invoke-OpenClawContainerPathValidation.ps1`) returns `OverallResult: Unexpected` on a healthy stack because its `DashboardAuth` probe POSTs to `/auth/verify` and receives HTTP 404; that path was explicitly acknowledged as an unverified guess in the source code at the time it was written, and the upstream gateway's actual authentication mechanism is a WebSocket device-pairing handshake rather than any REST POST surface, so no correct default path exists for this probe. The operator-approved fixes are: pre-install `@zed-industries/codex-acp@0.11.1` globally in the Dockerfile (Option 2A), and remove the `DashboardAuth` probe surface entirely from the validator (Option 1B).

---

## 2. Evidence

### 2.1 Agent startup log (2026-04-21T13:40:58Z)

Source: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md:46-49`

```
[gateway] ready (5 plugins: acpx, browser, device-pair, phone-control, talk-voice; 4.8s)
[plugins] embedded acpx runtime backend probe failed: embedded ACP runtime probe failed
  (agent=codex; command=npx @zed-industries/codex-acp@^0.11.1; cwd=/workspace; ACP connection closed)
```

The gateway initializes five plugins including `acpx`. The `acpx` embedded runtime probe then immediately fails. The command shown is `npx @zed-industries/codex-acp@^0.11.1`; this is the only reference to the package in the repository (see section 2.3 below).

### 2.2 In-container npx failure reproduction

Source: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md:54-58`

```
$ docker compose exec openclaw-agent sh -c 'cd /workspace && npx --yes @zed-industries/codex-acp@^0.11.1 --help'
npm error code ENOENT
npm error syscall mkdir
npm error path /.npm
npm error enoent This is related to npm not being able to find a file.
```

`npx` fails before fetching any code because it cannot create its cache directory at `/.npm`. The container's filesystem is read-only. The available writable tmpfs mounts are `/tmp` and `/.openclaw`; both carry `noexec,nosuid,nodev` flags, which would prevent execution of a fetched binary regardless.

### 2.3 Container hardening declarations (verified)

Source: `docker-compose.yml` (not shown inline; confirmed present on the branch per the issue doc at `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md:61`)

The agent runs:
- `user: "1654:1654"`
- `read_only: true`
- `cap_drop: [ALL]`
- `security_opt: [no-new-privileges:true]`
- tmpfs `/tmp:size=64m,noexec,nosuid,nodev`
- tmpfs `/.openclaw:size=64m,noexec,nosuid,nodev`

These declarations are confirmed to remain unchanged by Option 2A; AC-5 in the issue doc requires they are preserved.

### 2.4 Dockerfile — no npm install step (verified)

Source: `deploy/docker/openclaw-agent.Dockerfile` (read in this session)

```dockerfile
ARG OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest
FROM ${OPENCLAW_AGENT_IMAGE}

COPY --chown=1654:1654 deploy/docker/openclaw-assistant /opt/openclaw-assistant-seed
COPY --chown=1654:1654 deploy/docker/openclaw-agent-entrypoint.sh /usr/local/bin/openclaw-agent-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/openclaw-agent-entrypoint.sh"]
CMD ["node","openclaw.mjs","gateway","--allow-unconfigured"]
```

The Dockerfile (6 lines excluding the syntax comment) contains no `RUN npm install` step. There is no pre-installed `@zed-industries/codex-acp` package anywhere in the image build. The `RUN npm install -g @zed-industries/codex-acp@0.11.1` line must be added between the `FROM` line and the first `COPY` line.

### 2.5 Package references in the repository (verified)

Source: Grep for `codex-acp|@zed-industries` across the full repository (run in this session)

Matches are confined to `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md` only. There are no `package.json`, `package-lock.json`, or other lockfile references to `@zed-industries/codex-acp` in this repository. The version `^0.11.1` appears only in the startup log excerpt quoted in the issue doc. No additional files require a version bump in lockstep with the Dockerfile change.

### 2.6 Validator output (2026-04-21T13:54:56Z)

Source: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md:65-73`

```
OverallResult: Unexpected
Ready: status=ready, sqliteReady=true, hostAdapterReachable=true
CoreStatus: bridge.state=ready, outlookConnected=true, cacheStale=false
AgentDashboard: 200
AgentReadyz: 200 {"ready":true}
HostAdapterInContainer: IsExpected=True (in-container curl → HTTP 200)
DashboardAuth: IsExpected=False, HTTP 404 Not Found at /auth/verify
```

Every probe except `DashboardAuth` passes. The single failure drives `OverallResult` to `Unexpected`.

### 2.7 Acknowledged unverified path in source

Source: `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1:351-352`

```powershell
# Default '/auth/verify' is unverified against upstream config; tracked as a manual pre-release verification gate in docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/followups.md
[string]$AuthPath = '/auth/verify'
```

This comment was written at the time the probe was introduced (issue #38 remediation). The path was never verified against the upstream gateway; the fix decision treats this as confirmation that no valid default exists.

### 2.8 Upstream auth mechanism is WebSocket device-pairing, not REST POST

Source: `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/issue.md:79`

> The upstream `openclaw-agent` uses WebSocket device-pairing for authentication (visible in the same logs as `device_token_mismatch` → `device pairing auto-approved`), not a REST POST. No correct default path exists; retaining the probe as a dead surface invites future misdiagnosis.

The gateway startup log shows `device_token_mismatch` followed by `[gateway] device pairing auto-approved`. There is no `/auth/*` REST surface in the upstream image.

---

## 3. File Map — DashboardAuth Symbol References

### 3a. Production code to modify

| File | Lines | Symbol |
|---|---|---|
| `scripts/Invoke-OpenClawContainerPathValidation.ps1` | 18-20 (`.PARAMETER DashboardAuthPath` doc), 39 (`$DashboardAuthPath` param), 257 (call site), 265 (`$dashboardAuth` in array), 287 (`DashboardAuth = $dashboardAuth`) | `DashboardAuth`, `DashboardAuthPath`, `Invoke-OpenClawDashboardAuthProbe` |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1` | 338-396 (function body), 410 (export list) | `Invoke-OpenClawDashboardAuthProbe`, `DashboardAuth` |
| `scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psd1` | 22 (`FunctionsToExport`) | `Invoke-OpenClawDashboardAuthProbe` |

### 3b. Tests to modify

| File | Lines | Nature |
|---|---|---|
| `tests/scripts/Invoke-OpenClawContainerPathValidation.DashboardAuth.Tests.ps1` | 1-177 (entire file) | Five `It` blocks dedicated to `DashboardAuth`; entire file is removable |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` | 51, 101, 130, 189, 264, 307, 335 | Mock stubs routing `/auth/verify`; assertion on `$result.DashboardAuth.IsExpected`; comment naming `DashboardAuth` as a failing probe count |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.HostAdapter.Tests.ps1` | 38, 92, 139 | Mock stubs routing `/auth/verify` to satisfy the probe during unrelated `HostAdapter` tests |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.TokenPresence.Tests.ps1` | 38 | Mock stub routing `/auth/verify` |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Readyz.Tests.ps1` | 40, 64, 89 | Mock stubs routing `/auth/verify` |
| `tests/scripts/Install.Helpers.Tests.ps1` | — | No match; listed here for completeness after Grep confirmed no match |

### 3c. Archived audit artifacts to leave alone

All files under `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/` are read-only historical audit records. The following contain matches and must not be modified:

- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md`
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/plan.2026-04-20T09-21.md`
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/pr-notes.md`
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/code-review.2026-04-21T01-00.md`
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/feature-audit.2026-04-21T01-00.md`
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/policy-audit.2026-04-21T01-00.md`
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/evidence/issue-updates/issue-38.2026-04-20T09-21.md`
- `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/audit-2026-04-21T00-00/` (all files)

### 3d. Docs to update

| File | Lines | Required change |
|---|---|---|
| `docs/mailbridge-runbook.md` | 472 (`DashboardAuth` is expected when...) | Remove or replace the `DashboardAuth` row from the probe-expectation list |
| `docs/mailbridge-runbook.md` | 602-604 (section "Validation-script dashboard-auth overrides") | Remove the entire subsection; `-DashboardAuthPath` and `Invoke-OpenClawDashboardAuthProbe` will no longer exist |

---

## 4. Toolchain and Gate Checklist

### 4a. PowerShell toolchain (PoshQC — from `.claude/rules/powershell.md`)

Run in order; restart from step 1 if any step fails or modifies a file:

1. **Format**: `mcp__drmCopilotExtension__run_poshqc_format`
2. **Analyze**: `mcp__drmCopilotExtension__run_poshqc_analyze` (optional autofix: `mcp__drmCopilotExtension__run_poshqc_analyze_autofix`)
3. **Test**: `mcp__drmCopilotExtension__run_poshqc_test` (config: `scripts/powershell/PoshQC/settings/pester.runsettings.psd1`)

Coverage thresholds (from `.claude/rules/general-unit-test.md` and `powershell.md`):
- Repository-wide Pester line coverage: >= 80%
- `OpenClawContainerValidation` module (changed module): >= 90%
- Coverage regression on changed lines is a blocking finding

AC-6 requires PoshQC output committed under `docs/features/active/2026-04-21-openclaw-agent-capabilities-none/evidence/`.
AC-7 requires both thresholds to pass.

### 4b. Docker build and runtime verification (AC-1, AC-4, AC-5)

```bash
# Rebuild the agent image after the Dockerfile change
docker compose build openclaw-agent

# Recreate the container from the new image
docker compose up -d --force-recreate openclaw-agent

# AC-4: confirm global install is present
docker compose exec openclaw-agent sh -c \
  'ls /usr/local/lib/node_modules/@zed-industries/codex-acp/package.json'

# AC-1: confirm no probe-failed line in startup logs
docker compose logs openclaw-agent | grep 'embedded acpx runtime backend probe failed'
# Expected: no output

# AC-5: diff must show no changes to hardening declarations in docker-compose.yml
git diff docker-compose.yml
```

---

## 5. Risk Notes

### 5.1 npx auto-detection of a globally installed package (unverified)

The Option 2A fix installs `@zed-industries/codex-acp@0.11.1` via `npm install -g`, which places the binary at `/usr/local/lib/node_modules/@zed-industries/codex-acp/` and a symlink at `/usr/local/bin/`. When the gateway executes `npx @zed-industries/codex-acp@^0.11.1`, `npx` checks the local project's `node_modules`, then the global prefix. If `npx` resolves to the globally-installed package without attempting a registry fetch or cache write, the `read_only` constraint is satisfied. However, `npx` behavior with regard to semver range matching against globally installed packages varies across npm versions; if the installed version (`0.11.1`) satisfies the range (`^0.11.1`) but `npx` still attempts a registry check before resolving locally, the probe could fail in environments without outbound internet access.

**Verification step during execution**: after the image is built, run `docker compose exec openclaw-agent sh -c 'npx @zed-industries/codex-acp@^0.11.1 --version'` inside the hardened container (with `read_only` and `noexec` active) and confirm it exits zero without producing npm errors. If `npx` does not auto-detect the global package, the Dockerfile can be extended with `ENV NPM_CONFIG_PREFER_OFFLINE=true` or the `CMD` overridden to invoke the binary directly by its resolved path rather than through `npx`. Do not treat this as a blocker for planning; treat it as a gate that must pass before AC-1 can be claimed.

### 5.2 Version pin vs. range

The startup log records the range `^0.11.1`. Option 2A pins the install to `0.11.1`. If `npx` selects the globally-installed exact version and the gateway's version check accepts `0.11.1` as satisfying `^0.11.1`, there is no issue. If the gateway performs a strict equality check on the command string and rejects `0.11.1` in favor of fetching a newer patch, the runtime environment would need to be modified. This is considered low risk given that `^0.11.1` semantically includes `0.11.1`, but it is unverified against the upstream gateway binary.
