# Research: admin-access-automation (Issue #148)

- Date: 2026-07-11T20-00
- Author: task-researcher
- Feature: docs/features/active/2026-07-11-admin-access-automation-148/
- Epic: openclaw-runtime-remediation (child C, wave 1); depends on child B installer-image-version-alignment
- Mode: preparation-only research (no implementation)

Canonical issue number for all cross-references: **#148**. (The epic manifest in
`docs/features/epics/openclaw-runtime-remediation/epic.md` lines 33-35 records a
placeholder `issue_num: 903` for this child; per the delegation, #148 is
authoritative and is used throughout this note.)

## Scope Recap

Provision three capabilities plus a committed runbook, without generating tokens:
1. Gateway-token delivery to the Control UI via the `#token=` URL fragment.
2. Device-token (HostAdapter bearer token) rotation/reissue.
3. `web_search` provider provisioning in the OpenClaw agent configuration.
4. A committed runbook for the human-held-secret steps that cannot be automated.

Token generation is out of scope; it is already handled by
`scripts/Invoke-OpenClawAgentOnboarding.ps1`.

---

## A. Current Onboarding / Token Surface (verified)

### A.1 `scripts/Invoke-OpenClawAgentOnboarding.ps1`
- The current script (149 lines) does not run a container. It generates a
  base64url-encoded random token from `System.Security.Cryptography.RandomNumberGenerator`
  (`New-OpenClawGatewayToken`, lines 76-101) and writes `OPENCLAW_GATEWAY_TOKEN`
  into the target `.env` (`Set-OpenClawEnvEntry`, lines 103-136; main flow lines 138-148).
- Idempotency: if `OPENCLAW_GATEWAY_TOKEN` is already present and non-empty, it
  returns without changes unless `-Force` is supplied (lines 140-144).
- `-EnvFilePath` default `./.env`; `-TokenByteLength` default 48 (ValidateRange 24-128);
  `SupportsShouldProcess` on the state-changing entry writer.
- NOTES (lines 32-34): `ANTHROPIC_API_KEY` is NOT the gateway token; operators
  supply the Anthropic key separately in `secrets/.env.anthropic` (human-held).
- Discrepancy to flag (not in scope to fix here): README.md lines 666-672 describe
  this script as executing `openclaw onboard` in a throwaway container with an
  `-OnboardBinaryPath` parameter. The committed script does neither — it uses local
  RNG and has no such parameter. The spec/plan should treat the committed script as
  authoritative and not rely on the README's description of onboarding internals.

### A.2 `deploy/docker/openclaw-assistant/openclaw.json` (baked seed)
- `gateway.mode="local"`, `gateway.port=18789`, `gateway.bind="auto"` (lines 2-4).
- `gateway.auth.mode="token"`, `gateway.auth.token="${OPENCLAW_GATEWAY_TOKEN}"`
  (lines 5-9) — SecretRef-style env interpolation.
- `tools.profile="coding"` (lines 14-16). Per the archived #43 v2 research,
  `coding` includes `group:web` which contains `web_search`, `x_search`, `web_fetch`
  (see section D).
- `agents.list[0]` id `admin-assistant`, workspace `/workspace`, skill `mailbridge_admin`
  (lines 26-34).

### A.3 How Compose passes the gateway token
- `docker-compose.yml` line 75: `openclaw-agent` receives
  `OPENCLAW_GATEWAY_TOKEN: ${OPENCLAW_GATEWAY_TOKEN}` from the project `.env`.
- Agent port publish: `docker-compose.yml` line 78,
  `"127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}:18789"` (loopback-bound).
- No `.env` / `.env.example` file is tracked in the repo (Glob for `**/*.env*`
  returned nothing; these are gitignored). README (line 455) and the runbook
  (`docs/mailbridge-runbook.md` line 455) instruct operators to copy `.env.example`
  to `.env`, but that template is not present in this worktree. The plan must not
  assume a tracked `.env.example`.

---

## B. Control UI Token-Fragment Handling (verified from repo docs; behavior is upstream)

### B.1 What the Control UI is
- The Control UI (operator dashboard) is served by the `openclaw-agent` service
  (the upstream OpenClaw runtime, built from `ghcr.io/openclaw/openclaw:latest`;
  README lines 660-664). It is NOT the repository-owned `OpenClaw.Core` UI.
- URL: `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/` (README lines 235, 706;
  runbook line 22). Loopback-bound on port 18789.

### B.2 Fragment consumption
- The `#token=` fragment behavior is defined by the upstream OpenClaw Control UI
  image (documented against the "OpenClaw 2026.6.11 build"), not by repo code. No
  repo source parses the fragment; the repo only documents the operator procedure:
  - README lines 706-711 and runbook lines 636-638: authenticate by opening
    `http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/#token=<OPENCLAW_GATEWAY_TOKEN>`,
    or open the root and paste the token into Control UI settings.
  - Loading the root without the fragment/pasted token confirms only that the UI
    is served, not operator authentication (README line 711; runbook lines 492, 638).
- Web confirmation attempt: `docs.openclaw.ai/gateway/configuration-reference` did
  not document fragment handling. Repo evidence (README + runbook, matched to the
  2026.6.11 build) is treated as sufficient for the URL shape; the fragment-parsing
  implementation remains upstream and version-dependent.
- Version caveat: `OPENCLAW_AGENT_IMAGE` defaults to a floating `:latest` tag, so
  the auth flow can change across upgrades (README line 715). This is exactly why
  the feature depends on child B (matched image versions; section E).

### B.3 Exact URL shape an automation would construct
```
http://127.0.0.1:<agentPort>/#token=<OPENCLAW_GATEWAY_TOKEN>
```
where `<agentPort>` resolves from `OPENCLAW_AGENT_PORT` (default 18789) read from
the target `.env`, and `<OPENCLAW_GATEWAY_TOKEN>` is read from the same `.env`.
The token is base64url (no `+`, `/`, `=`), so it is already fragment-safe and does
not require percent-encoding.

### B.4 Device re-pair coupling
- Operator sessions are bound via device pairing. When `openclaw-agent` is recreated
  (e.g., image upgrade), the old pairing is rejected. Re-pair procedure (README
  line 713): run `openclaw devices clear` inside the agent container, clear browser
  site data for the dashboard origin, then reopen the `#token=` URL. This ties the
  gateway-token delivery capability to a browser step that automation cannot perform
  unattended (see Automation Feasibility).

---

## C. Device-Token Storage (verified) — distinct from the gateway token

### C.1 What it is and where it lives
- The HostAdapter device/bearer token is a file on the Windows host at
  `C:\ProgramData\OpenClaw\HostAdapter\adapter.token` (runbook lines 401-413).
- The env var `HOSTADAPTER_TOKEN_FILE` points to that host path
  (runbook line 458: `HOSTADAPTER_TOKEN_FILE=C:\ProgramData\OpenClaw\HostAdapter\adapter.token`).
- Compose bind-mounts it read-only into containers at `/run/openclaw/hostadapter.token`:
  - `docker-compose.yml` lines 41-44 (openclaw-core), lines 80-83 (openclaw-agent).
  - `docker-compose.dev.yml` lines 20-23 (openclaw-dev).

### C.2 How it is produced today
- Created manually by the operator (runbook C.1, lines 399-413):
  `Set-Content C:\ProgramData\OpenClaw\HostAdapter\adapter.token -Value '<real-secret>'`.
  Explicitly "not a one-time placeholder"; the operator must supply a real long
  random secret and keep the file.
- The HostAdapter reads it via `appsettings.json` `OpenClaw:HostAdapter:TokenFilePath`
  (runbook lines 415-427).

### C.3 How it is consumed
- Containers set `OpenClaw__HostAdapter__TokenFile=/run/openclaw/hostadapter.token`
  (`docker-compose.yml` lines 28, 74; `docker-compose.dev.yml` line 15).
- The agent's tools read the token from `/run/openclaw/hostadapter.token` and send
  `Authorization: Bearer <token>` to `http://host.docker.internal:4319/v1/...`
  (`deploy/docker/openclaw-assistant/TOOLS.md` lines 7-8 and every tool's Headers).
- The in-container validation probe reads the same file and calls `/status` with a
  bearer header (`OpenClawContainerValidation.psm1`
  `Invoke-OpenClawHostAdapterInContainerProbe`, lines 315-351).

### C.4 What rotation/reissue requires
- Both ends must agree on the same value. The runbook already states the manual
  procedure (lines 412, 700): regenerate `adapter.token` on the host, then restart
  the HostAdapter AND the Core container (and, for the agent path, the agent
  container) so both sides read the current token. Bind mounts are file binds; the
  running processes read the file at startup, so a content change requires a restart
  of every consumer.
- Rotation therefore = (1) write new secret to the host token file (state-changing,
  needs ShouldProcess), (2) restart HostAdapter + `openclaw-core` + `openclaw-agent`,
  (3) old token is invalidated once every consumer has restarted. The old value is
  invalidated implicitly by overwrite; there is no separate revocation endpoint.

### C.5 Gateway-token vs device-token distinction (explicit)
| Aspect | Gateway token | Device (HostAdapter) token |
|---|---|---|
| Env var | `OPENCLAW_GATEWAY_TOKEN` | file at `HOSTADAPTER_TOKEN_FILE` |
| Storage | value in project `.env` | file `C:\ProgramData\OpenClaw\HostAdapter\adapter.token` |
| Authenticates | operator/browser -> Control UI/gateway | container agent/core -> HostAdapter |
| Endpoint / port | agent gateway, port 18789 | HostAdapter, port 4319 |
| Consumed via | `openclaw.json` `gateway.auth.token`; `#token=` fragment | `Authorization: Bearer` header; `OpenClaw__HostAdapter__TokenFile` |
| Generated by | `Invoke-OpenClawAgentOnboarding.ps1` (RNG) | operator-supplied (manual today) |
| This feature | delivery only (capability 1) | rotation/reissue (capability 2) |

---

## D. `web_search` Provider Configuration

### D.1 Repo state
- `web_search` is a member of `group:web` (with `x_search`, `web_fetch`), which is
  included by the `coding` tools profile that `openclaw.json` already sets
  (`tools.profile="coding"`, openclaw.json lines 14-16). Source: archived #43 v2
  research `docs/features/archive/2026-04-21-openclaw-agent-capabilities-none-43/v2/2026-04-22-openclaw-agent-capabilities-none-v2-research.md`
  lines 40-78, which cites `docs.openclaw.ai/gateway/configuration-reference`.
- So the `web_search` tool is already enabled at the profile level. What is NOT
  configured in the repo is the underlying search-provider credential/endpoint the
  tool calls. No `web_search` provider block or `plugins.entries.*` section exists
  in `deploy/docker/openclaw-assistant/openclaw.json`.

### D.2 What provisioning entails (partly upstream-defined)
- Web confirmation (`docs.openclaw.ai/gateway/configuration-reference`): the upstream
  gateway configures web providers under `plugins.entries.<provider>.config`
  (Firecrawl was the example returned), with a `webSearch.apiKey` field that
  "accepts SecretRef" and a related `webFetch.apiKey`. This indicates `web_search`
  provisioning means adding a provider entry to the agent config plus supplying a
  provider API key via SecretRef (i.e., an env-interpolated `${...}` value backed by
  the `.env` / secrets, mirroring how `OPENCLAW_GATEWAY_TOKEN` is referenced).
- The exact provider name/keys are upstream and version-dependent; repo evidence is
  insufficient to pin the precise schema. The plan should treat the provisioning
  shape as: (a) add a provider entry under the tools/plugins section of
  `openclaw.json`, (b) reference the provider key via a `${WEB_SEARCH_API_KEY}`-style
  SecretRef, (c) supply that env var through compose/`.env`, matching the existing
  `OPENCLAW_GATEWAY_TOKEN` pattern. The concrete key names must be confirmed against
  the child-B-aligned image at plan time.

### D.3 Is the secret human-held?
- Yes. A search-provider API key (Firecrawl or equivalent) is issued by an external
  SaaS and must be supplied by the operator. It cannot be generated locally like the
  gateway token. This is a human-held secret and belongs in the runbook.

---

## E. Upstream Dependency on Child B (installer-image-version-alignment)

- The epic records exactly one dependency edge:
  `admin-access-automation -> installer-image-version-alignment`
  (`docs/features/epics/openclaw-runtime-remediation/epic.md` lines 30-35, 91-99;
  child C is wave 1, child B is wave 0).
- Child B aligns the installer's Control UI and gateway container image versions in
  `Install.ps1` and the `OpenClawContainerValidation` module, keeping consistency
  with the #142/#144 fixes (epic.md lines 53-57).
- Relevance to #148: the `#token=` fragment behavior and the Control UI URL/port are
  version-dependent (README line 715, floating `:latest` tag). Admin-access automation
  runs against B's version-aligned staging behavior with **matched Control UI and
  gateway images**, so the URL shape and fragment behavior are stable at execution
  time. Where the plan pins the fragment behavior or provider schema, it must cite
  that B provides matched images rather than re-deriving version behavior here.
- B is prepared concurrently; its spec/plan live under
  `docs/features/active/installer-image-version-alignment-<issue>/` and are fanned in
  before execution. Do NOT assume B's files exist in this worktree. The #148 plan
  should reference B's matched-image guarantee as an input assumption, not read B's
  artifacts directly.

---

## F. Repository Policy Constraints (spec/plan must honor)

From `.claude/rules/powershell.md`, `.claude/rules/general-code-change.md`,
`.claude/rules/general-unit-test.md`, `.claude/rules/quality-tiers.md`:

- **Tier:** This feature is T1 for the token-handling paths (delivery + device-token
  rotation). Epic assessed it complexity band C3, floor-forced by
  `auth_or_token_handling` (epic.md line 87).
- **PowerShell 7+**, advanced functions with `CmdletBinding()`, named parameters,
  validation attributes.
- **ShouldProcess/SupportsShouldProcess** required for every state-changing action
  (writing the token file, restarting containers, editing `openclaw.json`).
- **Wrapper-function seams** for external executables: never mock `docker`/`gh`/`git`
  directly; extract into `Invoke-<Tool>Exe -<Tool>Args`-style wrappers. The existing
  `Invoke-OpenClawDockerCommand` seam in `OpenClawContainerValidation.psm1`
  (lines 184-209) is the established docker seam to reuse for any restart/exec.
- **No plaintext secret logging, no hard-coded tokens.** Tokens read from files/`.env`
  must never be echoed to output/verbose. Base64url tokens are fragment-safe.
- **File size <= 500 lines** per production/test/script file.
- **Change budget:** direct mode up to 2 production PS files (+ tests); per-batch cap
  3 production + 3 test files. Larger scope routes to `powershell-orchestrator`. The
  three capabilities plus runbook likely exceed a single direct-mode batch and should
  be planned as separate cohesive scripts/modules.
- **Tests:** Pester v5, mirrored under `tests/scripts/...`, `*.Tests.ps1`, one behavior
  per `It`, mock the wrapper seams not the executables, deterministic (no network,
  no temp files, no sleeps). Coverage line >= 85% / branch >= 75% uniform across tiers.
- **Toolchain loop:** format (PoshQC) -> analyze (PSScriptAnalyzer) -> test (Pester),
  restart on any change. Use the MCP PoshQC functions.

---

## Candidate Approaches

### Approach 1 (recommended): three focused advanced-function scripts + a committed runbook, reusing existing seams
- Capability 1 (delivery): a pure URL-composition function
  `Get-OpenClawControlUiTokenUrl` (or similar) that reads `OPENCLAW_GATEWAY_TOKEN`
  and `OPENCLAW_AGENT_PORT` from the target `.env` (reuse `Get-OpenClawEnvFileMap`
  from `OpenClawContainerValidation.psm1`) and returns the `#token=` URL. Emitting
  the URL is unattended; opening a browser is not automated (see feasibility).
- Capability 2 (rotation): `Set-OpenClawHostAdapterToken` (or
  `Invoke-OpenClawDeviceTokenRotation`) with `SupportsShouldProcess`, which writes a
  new RNG-generated secret to the host token file (reusing the RNG approach already
  in the onboarding script), then restarts HostAdapter + `openclaw-core` +
  `openclaw-agent` through the `Invoke-OpenClawDockerCommand` seam. Idempotent via a
  `-Force`-style guard mirroring the onboarding script.
- Capability 3 (provisioning): a function that edits/validates the `web_search`
  provider entry in `openclaw.json` (seed file, since the entrypoint re-seeds
  `/.openclaw/openclaw.json` on every start — `openclaw-agent-entrypoint.sh` line 34)
  and references the provider key via SecretRef; the API key itself stays in
  `.env`/secrets and is documented in the runbook.
- Runbook: extend `docs/mailbridge-runbook.md` (already the canonical operator
  runbook covering token files, Control UI auth, and restarts) with the human-held
  steps, or add a dedicated admin-access runbook cross-linked from it.
- Rationale: matches existing repo structure (validation module seams, onboarding
  RNG pattern, runbook), keeps each script small and single-purpose, honors the
  wrapper-seam and ShouldProcess rules, and isolates the human-held-secret steps.

### Rejected alternatives (brief)
- Single monolithic "provision admin access" script: would exceed the 500-line cap
  and the direct-mode change budget, and would couple three independently testable
  concerns. Rejected.
- Automating the browser step for `#token=` delivery (headless browser / device
  pairing automation): the pairing handshake and browser site-data step are
  upstream/interactive and not scriptable through repo seams; adds a heavy dependency
  and remains non-deterministic. Rejected in favor of producing the URL for handoff
  and documenting the browser step in the runbook.

---

## Automation Feasibility (MANDATORY)

Classification per capability and step: **fully automatable** (unattended script),
**partially automatable**, or **human-interaction-required**.

### Capability 1 — Gateway-token delivery via `#token=`
- Read token + port from `.env` and construct the `#token=` URL: **fully automatable**
  (pure function over `.env`; no secret needs to leave the host).
- Open the URL in a browser to authenticate the operator session: **human-interaction-required**.
  The Control UI parses the fragment client-side in a browser and completes device
  pairing (README lines 706-713). Automation can produce/hand off the URL but cannot
  complete the browser-side authentication unattended.
- Device re-pair after container recreation (`openclaw devices clear` + clear browser
  site data + reopen URL): **partially automatable**. `openclaw devices clear` can run
  via the docker exec seam unattended; clearing browser site data and reopening the
  URL are **human-interaction-required**.

### Capability 2 — Device-token rotation/reissue
- Generate a new secret and overwrite the host token file: **fully automatable**
  (RNG + file write with ShouldProcess).
- Restart HostAdapter + `openclaw-core` + `openclaw-agent` so both ends read the new
  value: **fully automatable** for the two containers via the docker seam. Restarting
  the HostAdapter process on the Windows host is automatable only if it runs as a
  managed service/task; if it is started interactively via `dotnet run`
  (runbook lines 432-434), the restart is **partially automatable** / operator-driven.
- Net: rotation can run unattended for the container consumers; the HostAdapter
  restart is the one step whose automation depends on how HostAdapter is hosted, and
  the runbook must cover the interactive-launch case.

### Capability 3 — `web_search` provider provisioning
- Add/validate the provider entry in `openclaw.json` referencing a SecretRef:
  **fully automatable**.
- Supply the search-provider API key (Firecrawl or equivalent): **human-interaction-required**.
  The key is issued by an external SaaS, cannot be generated locally, and must be
  placed by the operator into `.env`/secrets. This is a human-held secret.

### Anthropic / provider keys
- `ANTHROPIC_API_KEY` is human-held: supplied by the operator in
  `secrets/.env.anthropic` (onboarding script NOTES lines 32-34; consumed via
  `docker-compose.yml` line 62 `env_file: ./secrets/.env.anthropic`). Not generated
  by automation.
- The `web_search` provider key is likewise human-held (section D.3).

### Enumerated human-held-secret / human-interaction steps for the committed runbook
1. Open the `#token=` URL in a browser to complete Control UI operator authentication.
2. After container recreation, clear browser site data for the dashboard origin and
   reopen the `#token=` URL to re-pair (paired with the automatable
   `openclaw devices clear`).
3. Supply the search-provider API key (Firecrawl/equivalent) into `.env`/secrets as a
   SecretRef for `web_search` provisioning.
4. Supply the Anthropic API key in `secrets/.env.anthropic`.
5. Provide/keep the initial HostAdapter device-token secret and, where HostAdapter is
   launched interactively, restart it during rotation.
6. Provision the initial HostAdapter token file value on the host
   (`C:\ProgramData\OpenClaw\HostAdapter\adapter.token`) if it does not already exist.

---

## Behavior Semantics (success / failure / ordering / edge cases)

- Delivery success: a `#token=` URL is produced from a present, non-empty
  `OPENCLAW_GATEWAY_TOKEN` and the resolved agent port. Failure: missing/empty token
  (surface a clear error pointing to the onboarding script, mirroring
  `Test-OpenClawGatewayTokenPresence` messaging). Edge: token already base64url — no
  encoding needed; do not log the token value.
- Rotation success: host token file contains a new non-empty secret AND every
  consumer has been restarted so all read the same value. Failure modes: unreadable/
  unwritable token file, docker restart failure, HostAdapter not restarted (old token
  still in use -> 401). Ordering: write file, then restart consumers; old token
  invalid only after all consumers restart. Idempotency: re-running without `-Force`
  should not rotate an already-valid token.
- Provisioning success: `openclaw.json` (seed) contains a valid `web_search` provider
  entry with a SecretRef and the referenced env var is present. Failure: invalid JSON,
  missing provider key. Idempotency: re-running yields the same config (no duplicate
  provider entries). Note the entrypoint re-seeds `/.openclaw/openclaw.json` from the
  baked seed on every start (`openclaw-agent-entrypoint.sh` line 34), so the change
  must be made in the seed file and the image rebuilt to persist.

---

## Requirements Mapping (acceptance criteria -> design)

| AC | Design element | Files likely to change |
|---|---|---|
| Gateway token delivered via `#token=` | URL-composition function reading `.env`; runbook browser step | new script under `scripts/` + tests under `tests/scripts/`; `docs/mailbridge-runbook.md` |
| Device token rotated/reissued (idempotent) | RNG write + ShouldProcess + docker-seam restarts | new script/module + tests; reuse `OpenClawContainerValidation.psm1` docker seam; `docs/mailbridge-runbook.md` |
| `web_search` provisioned | provider entry + SecretRef in seed config | `deploy/docker/openclaw-assistant/openclaw.json` (+ image rebuild); provisioning/validation script + tests |
| Committed runbook for human-held steps | enumerated human steps 1-6 above | `docs/mailbridge-runbook.md` (or dedicated admin-access runbook cross-linked) |
| Token generation out of scope | reuse `Invoke-OpenClawAgentOnboarding.ps1` unchanged | none |

Proposed state model (rotation): `absent -> written -> propagated(restarted)`;
exit gate = all consumers restarted. Provisioning: `unset -> configured -> validated`.

---

## Testing Implications (no test code written)

- Pester v5, mirrored under `tests/scripts/`, `*.Tests.ps1`, one behavior per `It`,
  Arrange-Act-Assert.
- Mock the docker wrapper seam (`Invoke-OpenClawDockerCommand`), never `docker`
  directly; mock signatures must match named params exactly.
- Cover: URL construction with valid/missing/empty token and default vs explicit port;
  rotation write + restart happy path, unwritable file, docker restart failure,
  idempotent no-op without `-Force`; provider provisioning valid/invalid JSON and
  idempotent re-run; secret-not-logged assertions (verify no token value appears in
  output/verbose streams).
- Use in-memory pseudo-files for `.env`/token file (as the existing onboarding tests
  do) — no temp files, no network, deterministic. RNG-dependent output must be tested
  for shape/charset, not exact value.
- Coverage: line >= 85%, branch >= 75%; no regression on changed lines.

---

## Files That Will Need to Change (summary)

- New: `scripts/` script(s) for (1) `#token=` URL delivery, (2) device-token rotation,
  (3) `web_search` provisioning/validation — likely split across cohesive files to
  respect the 500-line cap and change budget; corresponding `tests/scripts/*.Tests.ps1`.
- `deploy/docker/openclaw-assistant/openclaw.json` — add the `web_search` provider
  entry / SecretRef (seed file; requires image rebuild to persist).
- `docs/mailbridge-runbook.md` — add the committed human-held-secret runbook section
  (or a dedicated cross-linked admin-access runbook).
- Reuse (no change expected): `OpenClawContainerValidation.psm1` docker seam and
  `.env` parser; `Invoke-OpenClawAgentOnboarding.ps1` (token generation unchanged).

## Constraints / Invariants to Preserve

- No plaintext secret logging; no hard-coded tokens; ShouldProcess on all writes/
  restarts; wrapper seams for docker; files <= 500 lines; PS 7+; T1 rigor on token
  paths; idempotent delivery/rotation/provisioning; changes to agent config must be
  made in the baked seed (entrypoint re-seeds on start); do not assume tracked
  `.env`/`.env.example`; treat child B's matched-image guarantee as an input assumption.

## Research Artifact Path

- `C:\Users\DanMoisan\repos\open-claw-bridge\.claude\worktrees\agent-a7d6a7fdd9376442b\docs\features\active\2026-07-11-admin-access-automation-148\research\2026-07-11T20-00-admin-access-automation-research.md`
