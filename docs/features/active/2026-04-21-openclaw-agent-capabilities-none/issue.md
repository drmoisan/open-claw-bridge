# Bug — OpenClaw agent reports `capabilities=none`; container-path validator falsely reports `Unexpected`

- Promotion type: bug
- Branch: `bug/openclaw-agent-capabilities-none`
- Base branch: `development`
- Created (UTC): 2026-04-21
- Associated GitHub issue: *(not yet opened; track locally)*

## Summary

Two defects observed while exercising the installed OpenClaw solution end-to-end:

1. The OpenClaw assistant (`openclaw-agent` container) cannot execute tools against the HostAdapter. When asked a data question it self-reports `capabilities=none` and refuses to issue HTTP requests against `http://host.docker.internal:4319/v1/...`.
2. The repository's container-path validator `scripts/Invoke-OpenClawContainerPathValidation.ps1` returns `OverallResult: Unexpected` even though every dependency in the data pipeline is healthy. The single failing probe is `DashboardAuth`, which POSTs to `/auth/verify` and receives HTTP `404 Not Found`.

Neither defect prevents the bridge, HostAdapter, or Core from running; both prevent the solution from functioning for the operator.

## Acceptance Criteria

- [x] AC-1 — The `openclaw-agent` container starts with a tool-capable plugin runtime attached. Evidence: `docker-recreate-logs.2026-04-21T14-00.md` captures `[plugins] embedded acpx runtime backend ready` at `2026-04-21T14:54:23Z` on the post-fix container; `grep -c "embedded acpx runtime backend probe failed"` returns `0`.
- [x] AC-2 — `scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru` returns `OverallResult: Expected` when the stack is healthy. Evidence: `validator-expected.2026-04-21T14-00.md` — the Pester "returns expected when all container endpoints match their validation contracts" test passes with `$result.OverallResult -eq 'Expected'`, `EndpointDiagnostics.Count == 6`, and no `DashboardAuth` property.
- [x] AC-3 — The `DashboardAuth` probe is removed from the validator surface (function, call site, script parameter, result field, and tests). Evidence: `dashboard-auth-grep.2026-04-21T14-00.md` — zero production-code matches for `DashboardAuth`, `Invoke-OpenClawDashboardAuthProbe`, `/auth/verify`, `DashboardAuthPath` across the repository after excluding audit archives, research, and the active feature folder.
- [x] AC-4 — The agent container image embeds `@zed-industries/codex-acp@0.11.1` at a predictable path so `npx` at runtime does not require registry access or writable cache. Evidence: `codex-acp-embedded.2026-04-21T14-00.md` — `/usr/local/lib/node_modules/@zed-industries/codex-acp/package.json` shows `"version": "0.11.1"` inside the running container. (Note: the upstream gateway still invokes `npx @zed-industries/codex-acp@^0.11.1`; the writable `NPM_CONFIG_CACHE=/workspace/.npm-cache` and `CODEX_HOME=/workspace/.codex` env vars added in Phases 4B/4C make that invocation succeed without registry access after the first cached run.)
- [x] AC-5 — The agent container's existing hardening (`read_only: true`, `cap_drop: ALL`, `no-new-privileges: true`, `noexec`/`nosuid`/`nodev` on the tmpfs mounts) is preserved. Evidence: `compose-hardening.2026-04-21T14-00.md` — `git diff development HEAD -- docker-compose.yml` is empty and all six hardening tokens remain at their original lines.
- [x] AC-6 — PowerShell toolchain (format → analyze → test) runs clean on the changed files. Evidence: `final-poshqc-format.2026-04-21T14-00.md` (clean on re-check after one apply pass), `final-poshqc-analyze.2026-04-21T14-00.md` (0 errors, 0 warnings on changed files), `final-poshqc-test.2026-04-21T14-00.md` (181/181 pass).
- [x] AC-7 — Repository-wide Pester line coverage remains ≥ 80% and changed module coverage remains ≥ 90%. Evidence: `coverage-delta.2026-04-21T14-00.md` — post-change repo coverage 88.58% (≥ 80% threshold), `OpenClawContainerValidation.psm1` coverage 90.80% (≥ 90% threshold). No changed-line regression.
- [x] AC-8 — Operator runbook (`docs/mailbridge-runbook.md`) is updated where it mentioned `DashboardAuth` so post-change readers are not directed at a removed surface. Evidence: the `DashboardAuth` expectation bullet and the `Validation-script dashboard-auth overrides` subsection have been removed; P5-T1 and P5-T2 are checked off in the plan; repo-wide zero-match grep (AC-3) confirms no remaining runbook references.

## Scope amendments during execution

The original plan in `plan.2026-04-21T14-00.md` covered Phase 4 (Dockerfile RUN layer for codex-acp). During P5-T4 runtime verification, two additional root causes surfaced that the original plan did not anticipate:

1. **Phase 4B** — `codex-acp` wraps a Rust `codex` CLI that refuses to store config under `/tmp` and cannot write to the read-only root FS. Resolved by adding `ENV CODEX_HOME=/workspace/.codex` to the Dockerfile and a matching `mkdir -p` line in the entrypoint.
2. **Phase 4C** — Even with `codex-acp` installed globally, the upstream gateway still spawns `npx @zed-industries/codex-acp@^0.11.1`, and `npx` always consults the npm cache to resolve the version range. With `HOME=/` on a read-only FS, the default `/.npm` cache path cannot be created. Resolved by adding `ENV NPM_CONFIG_CACHE=/workspace/.npm-cache` and a matching `mkdir -p` line.

Both amendments stay within the original AC surface: they land in the same Dockerfile + entrypoint files already in Phase 4 scope, they preserve AC-5 hardening, and they are necessary conditions for AC-1. Neither amendment touches `docker-compose.yml`.

## Out of Scope

- Upstream changes to the `ghcr.io/openclaw/openclaw` image.
- Any change to the `HostAdapterInContainer` probe (working correctly).
- Dashboard authentication architecture itself — the probe removal does not change operator auth flow, only the validator's incorrect probe against it.
- The three active OpenClaw features unrelated to this bug.

## Evidence

### Assistant self-report (from operator conversation, 2026-04-21)

> I appreciate the question, but I'm unable to retrieve your calendar or email data right now. The tools I need to query the HostAdapter API (HTTP calls to http://host.docker.internal:4319/v1/...) require shell/exec capabilities, which aren't available in this session.
> My runtime capabilities show capabilities=none — I don't have access to execute HTTP requests against the HostAdapter bridge.

### Agent container startup (from `docker compose logs openclaw-agent`, 2026-04-21T13:40:58Z)

```
[gateway] ready (5 plugins: acpx, browser, device-pair, phone-control, talk-voice; 4.8s)
[plugins] embedded acpx runtime backend probe failed: embedded ACP runtime probe failed
  (agent=codex; command=npx @zed-industries/codex-acp@^0.11.1; cwd=/workspace; ACP connection closed)
```

### npx failure reproduced inside the agent container

```
$ docker compose exec openclaw-agent sh -c 'cd /workspace && npx --yes @zed-industries/codex-acp@^0.11.1 --help'
npm error code ENOENT
npm error syscall mkdir
npm error path /.npm
npm error enoent This is related to npm not being able to find a file.
```

Root cause: the container is `read_only: true`, user `1654` has no writable `$HOME`, and the available tmpfs mounts (`/tmp`, `/.openclaw`) are `noexec`. `npx` cannot write its cache or execute a fetched binary.

### Validator output (from `scripts/Invoke-OpenClawContainerPathValidation.ps1 -PassThru`, 2026-04-21T13:54:56Z)

```
OverallResult: Unexpected
Ready: status=ready, sqliteReady=true, hostAdapterReachable=true
CoreStatus: bridge.state=ready, outlookConnected=true, cacheStale=false
AgentDashboard: 200
AgentReadyz: 200 {"ready":true}
HostAdapterInContainer: IsExpected=True (in-container curl → HTTP 200)
DashboardAuth: IsExpected=False, HTTP 404 Not Found at /auth/verify
```

The script's module already documents `/auth/verify` as an unverified guess at [scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1:351-352](../../../../../scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1#L351-L352).

## Chosen Fix Options (operator approved)

- **Option 1B** — Remove the `DashboardAuth` probe surface entirely (not merely de-aggregate). The upstream `openclaw-agent` uses WebSocket device-pairing for authentication (visible in the same logs as `device_token_mismatch` → `device pairing auto-approved`), not a REST POST. No correct default path exists; retaining the probe as a dead surface invites future misdiagnosis.
- **Option 2A** — Pre-install `@zed-industries/codex-acp@0.11.1` globally in the `openclaw-agent` image so the embedded ACP runtime starts without runtime `npx` fetches or writable cache. Preserves `read_only: true`, `cap_drop: ALL`, and `noexec` tmpfs mounts.

## References

- Assistant skill contract: [deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md](../../../../deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md)
- Assistant tool contract: [deploy/docker/openclaw-assistant/TOOLS.md](../../../../deploy/docker/openclaw-assistant/TOOLS.md)
- Existing validator module: [scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1](../../../../scripts/powershell/modules/OpenClawContainerValidation/OpenClawContainerValidation.psm1)
- Existing validator script: [scripts/Invoke-OpenClawContainerPathValidation.ps1](../../../../scripts/Invoke-OpenClawContainerPathValidation.ps1)
- Prior remediation closure (sibling feature): [docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/](../2026-04-20-cannot-access-agent-in-docker-38/)
