# Delivery-Gap Analysis: Issue #38 — cannot-access-agent-in-docker

- Research date: 2026-04-20
- Branch: bug/cannot-access-agent-in-docker-38
- Researcher: task-researcher agent

---

## 1. Current-State Analysis of the Staged Fix

### 1.1 Staged-change inventory (by file)

| File | Change summary | Evidence |
|---|---|---|
| `README.md` step 3 | `up --build -d openclaw-core openclaw-agent` (both services at once) | Line 243 read directly |
| `README.md` step 4 heading | Renamed to "Manage the OpenClaw Assistant Service" | Line 267 |
| `README.md` step 4 body | Describes agent as a service sitting beside `openclaw-core`; asserts "no longer requires a separate gateway token for local access" | Lines 268-306 |
| `docs/mailbridge-runbook.md` Install Path C step 3 | Now invokes `up --build -d openclaw-core openclaw-agent` and refers to the validation script | Lines 436-462 |
| `docs/mailbridge-runbook.md` "Optional OpenClaw Assistant Service" section | Section **persists at line 475** with that exact heading and "optional" framing | Confirmed by grep |
| `docs/architecture-diagrams.md` | Agent node updated to `127.0.0.1:18789`; topology paragraph omits "optional" | Lines 25, 49 |
| `deploy/docker/openclaw-agent.Dockerfile` | New; builds local wrapper from `${OPENCLAW_AGENT_IMAGE}`, bakes seed files, sets `CMD ["node","openclaw.mjs","gateway","--allow-unconfigured"]` | File read directly |
| `deploy/docker/openclaw-agent-entrypoint.sh` | New; unconditionally copies all seed files into `/workspace` and `/.openclaw` on every start; execs `docker-entrypoint.sh "$@"` | File read directly |
| `deploy/docker/openclaw-assistant/openclaw.json` | `_placeholder` key removed; `gateway.bind` changed `loopback` → `auto`; `gateway.auth.mode` **remains `"token"`** | File read directly |
| `docker-compose.yml` `openclaw-agent` service | Changed from `image:` to `build:`; `/.openclaw` tmpfs carries `uid=1654,gid=1654`; `OPENCLAW_GATEWAY_TOKEN: ${OPENCLAW_GATEWAY_TOKEN:-openclaw-dev-token}` added; `OPENCLAW_AGENT_WORKSPACE` removed; named volume `openclaw_agent_workspace` added | Lines 52-93 |
| `docker-compose.dev.yml` `openclaw-agent` | Adds `extra_hosts: host.docker.internal:host-gateway`; dev compose repoints core to `OPENCLAW_DEV_HTTP_PORT:-8082` | Lines 29-31, 25 |
| `.env.example` | Read permission denied; contents inferred from compose plumbing and README; `OPENCLAW_GATEWAY_TOKEN=openclaw-dev-token` is the default in compose | Compose line 75 |
| `scripts/Invoke-OpenClawContainerPathValidation.ps1` | New 497-line script; checks Docker engine, Core and Agent container existence/running/health, Core `/health/live` `/health/ready` `/api/status`, Agent dashboard root `/` | File read directly |
| `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1` | New 238-line Pester test file; five test cases | File read directly |
| `deploy/docker/openclaw-core.Dockerfile` | Commentary-only changes; no behavior change | File read directly |

### 1.2 Work-stream delivery status

#### Stream 1 — Reframe agent as integral, not optional

**Partially delivered.**

- README: the agent section heading is now "Manage the OpenClaw Assistant Service" (not "Optional") and step 3 starts both services together. The word "optional" does not appear elsewhere in README. **Pass.**
- `docs/architecture-diagrams.md`: port updated to `18789`; description treats agent as a required peer. **Pass.**
- `docs/mailbridge-runbook.md`: **Fail on two counts.**
  1. The section heading at line 475 is still **"## Optional OpenClaw Assistant Service"**. The issue requires renaming this to "OpenClaw Agent (Required)" and folding it into the primary install path.
  2. The runbook purpose block at lines 9-10 and 20-21 still lists the HostAdapter/Docker path as "optional" and describes `openclaw-agent` as an additive service. These lines were not changed.
- `AGENTS.md`: confirmed untouched. The file is auto-generated and contains no service-topology content, so no `openclaw-agent` description is present. The issue asks for `AGENTS.md` to describe `openclaw-agent` as a required service. **Unaddressed.**

Remaining "optional" wording confirmed by grep:

- `docs/mailbridge-runbook.md:9` — "3. Optional additive `OpenClaw.HostAdapter` plus Docker `OpenClaw.Core`"
- `docs/mailbridge-runbook.md:20-21` — "is optional" (HostAdapter), "is optional" (Core)
- `docs/mailbridge-runbook.md:364` — "This path is optional and depends on a working Windows bridge installation."
- `docs/mailbridge-runbook.md:475` — **"## Optional OpenClaw Assistant Service"** (heading)

#### Stream 2 — Onboarding step that matches the upstream setup flow

**Not delivered; non-conforming path taken instead.**

The issue (and the canonical framing from the issue owner) requires a scripted onboarding step that runs the upstream `openclaw-gateway` onboarding sequence to generate a gateway token and write it to `.env`. The upstream manual flow, confirmed via `docs.openclaw.ai`, is:

```bash
docker compose run --rm --no-deps --entrypoint node openclaw-gateway \
  dist/index.js onboard --mode local --no-install-daemon
```

The implementers instead:

1. Built a local wrapper image that starts the gateway with `--allow-unconfigured`.
2. Added a hard-coded default `OPENCLAW_GATEWAY_TOKEN=openclaw-dev-token` to `docker-compose.yml`.
3. Documented in the runbook and README that "the dashboard no longer requires a separate gateway token for local access."

**The contradiction:** `deploy/docker/openclaw-assistant/openclaw.json` still has `"gateway.auth.mode": "token"`. The upstream configuration reference confirms `gateway.auth.token` maps to `${OPENCLAW_GATEWAY_TOKEN}`. A gateway started with `--allow-unconfigured` boots the process but does not override the `auth.mode` setting in the loaded config file. The runbook claim that "auth.mode is none" is not supported by the file on disk. The hard-coded `openclaw-dev-token` default is a placeholder that operators must know to replace; no script produces a real token, so the credential problem is shifted rather than solved.

**No `scripts/onboard-openclaw-agent.ps1` or equivalent exists.** The `scripts/` directory has no onboarding script. This is the central gap.

#### Stream 3 — Single verification script

**Partially delivered.** `Invoke-OpenClawContainerPathValidation.ps1` (497 lines) exists and covers checks 1, 2, and a partial version of check 5. The five sub-checks from the issue are mapped below:

| Issue sub-check | Implemented | Gap |
|---|---|---|
| 1. `docker compose ps openclaw-agent` reports `healthy` | Yes — `Invoke-OpenClawContainerValidation` checks `State.Health.Status == healthy` | None |
| 2. `GET /readyz` returns 200 | **No** — the script checks `GET /` (agent dashboard root), not `/readyz` | `/readyz` endpoint not probed |
| 3. In-container `GET http://host.docker.internal:4319/v1/status` with bearer token | **No** — no `docker compose exec` call exists in the script | HostAdapter reachability from inside container not verified |
| 4. `OPENCLAW_GATEWAY_TOKEN` present in `.env` and non-empty | **No** — no `.env` file read or token presence check | Token presence unverified |
| 5. Dashboard accepts the shared secret programmatically | **Partial** — `GET /` returning HTTP 200 is checked, but this is unauthenticated HTML and does not verify that the token is accepted | Auth probe missing |

Additionally: the runbook now refers to `Invoke-OpenClawContainerPathValidation.ps1` as the validation step; the old ad-hoc `curl` block has been replaced. **Pass** for integration into runbook.

#### Stream 4 — Port alignment

**Mostly delivered, with one confirmed bug.**

- `docker-compose.yml` line 77: `127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}:18789` — correct.
- `docker-compose.yml` line 38: `127.0.0.1:${OPENCLAW_HTTP_PORT:-8080}:8080` — correct.
- `docker-compose.dev.yml` line 25: `127.0.0.1:${OPENCLAW_DEV_HTTP_PORT:-8082}:8080` — correct for dev.
- `docs/architecture-diagrams.md`: `18789` — correct.
- README: no `8181` reference found.
- `docs/mailbridge-runbook.md`: no `8181` reference in live content (the stale `8181` existed in the prior runbook, but the staged changes replaced the old verification block with the script invocation). **Grep confirms no `8181` in runbook live text.**
- `docs/features/active/2026-04-16-openclaw-agent-docker-30/v1/` and `v2/`: still contain `8181` in archived spec and evidence files. These are historical artifacts; they should be audited to determine if they are still canonical or should be archived.

**Confirmed bug:** `scripts/Invoke-OpenClawContainerPathValidation.ps1` line 3 defaults `CoreBaseUrl` to `http://127.0.0.1:8081`. The production compose file publishes Core on `${OPENCLAW_HTTP_PORT:-8080}` (line 38 of `docker-compose.yml`). Port `8081` does not match any value in the compose file. The test file hardcodes all Core endpoint URLs with `8081` (lines 51-53, 78-80, 100-101, 105, 156-158). The correct default is `8080`. This is a bug that will cause the validation script to fail against a real deployment unless the operator passes `-CoreBaseUrl http://127.0.0.1:8080` explicitly.

---

## 2. Remaining Work Required to Close Issue #38

### 2.1 Onboarding script (central gap — blocker)

Add `scripts/Invoke-OpenClawAgentOnboarding.ps1`.

**What it must do:**

The upstream onboarding command (confirmed from `docs.openclaw.ai/install/docker.md`) is:

```bash
docker compose run --rm --no-deps --entrypoint node openclaw-gateway \
  dist/index.js onboard --mode local --no-install-daemon
```

The PowerShell equivalent for this repository's compose stack must:

1. Accept (or prompt for) the Anthropic API key. The upstream non-interactive flag confirmed at `docs.openclaw.ai/start/wizard-cli-automation.md` is `--non-interactive --anthropic-api-key "$ANTHROPIC_API_KEY"`.
2. Run the onboarding subcommand against `openclaw-agent` (the service name in this repo's compose file, which wraps `openclaw-gateway`). **Blocker noted below.**
3. Capture `OPENCLAW_GATEWAY_TOKEN` from the generated `.env` or from onboarding output and write it to the local `.env` file.
4. Return so the operator can follow with the normal `docker compose up -d` sequence.

**Blocker — `CMD` incompatibility:** The current `deploy/docker/openclaw-agent.Dockerfile` sets:

```dockerfile
CMD ["node","openclaw.mjs","gateway","--allow-unconfigured"]
```

The upstream onboarding entrypoint override is `--entrypoint node` with argument `dist/index.js onboard ...`. The wrapper Dockerfile does not override `ENTRYPOINT` from the upstream image to a fixed binary path; it chains through `docker-entrypoint.sh`. Whether `--entrypoint node` still resolves correctly through the wrapper depends on whether the upstream image's `docker-entrypoint.sh` passes arguments through or intercepts them. This must be verified before writing the onboarding script. If `docker-entrypoint.sh` does not pass through to `node` correctly, the Dockerfile must expose a direct `node` entrypoint path or the onboarding must be run against the upstream image directly (before the wrapper build), which changes the operational sequence.

**Blocker — binary path:** The Dockerfile CMD uses `openclaw.mjs`, but the upstream manual command uses `dist/index.js`. These may refer to the same file by two names, or the wrapper image may rename it. Verify the actual path by inspecting the upstream image layer or consulting the OpenClaw source.

### 2.2 Reconcile `openclaw.json` auth with onboarding output

File: `deploy/docker/openclaw-assistant/openclaw.json`

`gateway.auth.mode` is currently `"token"`. If onboarding produces `OPENCLAW_GATEWAY_TOKEN`, the gateway config must reference it via `gateway.auth.token: "${OPENCLAW_GATEWAY_TOKEN}"`. The upstream configuration reference shows this as `gateway: { auth: { token: "${OPENCLAW_GATEWAY_TOKEN}" } }`.

Remove the conflicting runbook and README assertions that "auth.mode is none" — those are false per the current file.

### 2.3 Fix `OPENCLAW_GATEWAY_TOKEN` in `.env.example` and `docker-compose.yml`

- `.env.example`: change `OPENCLAW_GATEWAY_TOKEN=openclaw-dev-token` to `OPENCLAW_GATEWAY_TOKEN=` (empty, populated by onboarding script) with a comment directing operators to run `scripts/Invoke-OpenClawAgentOnboarding.ps1` first.
- `docker-compose.yml` line 75: remove the `openclaw-dev-token` default so a misconfigured `.env` fails loudly rather than silently using a placeholder token.

### 2.4 Extend `Invoke-OpenClawContainerPathValidation.ps1`

File: `scripts/Invoke-OpenClawContainerPathValidation.ps1` (currently 497 lines — adding further functions risks breaching the 500-line limit; consider extracting helpers to a module).

Add:

1. **`/readyz` probe** — replace or supplement the current `GET /` agent dashboard check with `GET /readyz`. The issue and issue logs both show `/readyz` returning 200 as a meaningful signal distinct from the HTML dashboard.
2. **HostAdapter in-container reachability** — a `docker compose exec openclaw-agent sh -c 'curl ...'` call using `Invoke-OpenClawDockerCommand`.
3. **Token presence check** — read `.env` from the compose project directory and verify `OPENCLAW_GATEWAY_TOKEN` is set and non-empty.
4. **Auth probe** — post the gateway token to the appropriate auth endpoint and confirm the response code is 200 (not the credential prompt page).
5. **Fix `CoreBaseUrl` default** — change line 3 from `http://127.0.0.1:8081` to `http://127.0.0.1:8080`.

### 2.5 Fix the test file to match the corrected default

File: `tests/scripts/Invoke-OpenClawContainerPathValidation.Tests.ps1`

All hardcoded `8081` URI strings in the test file must change to `8080` when the script default is corrected. Additionally, new tests must cover the new checks added in 2.4.

### 2.6 Populate `spec.md`

File: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/spec.md`

These sections are currently empty stubs:

- **Scope & Non-Goals**: define in-scope (onboarding script, auth reconciliation, runbook reframing, validation script extensions) and out-of-scope (upstream image source changes, production multi-user deployments, non-Anthropic providers beyond the onboarding script's scope).
- **Proposed Fix**: populate all sub-sections with the design from this research (onboarding command, token flow, openclaw.json change, compose plumbing, validation extension).
- **Acceptance Criteria**: map each of the four work streams to a concrete, verifiable criterion; include the upstream onboarding contract as a criterion (token is generated by the script, written to `.env`, and accepted by the dashboard without manual edits).
- **Risks & Mitigations**: document the `docker-entrypoint.sh` passthrough risk, the 500-line script limit risk, and the token rotation/migration note.
- **Rollout & Follow-up**: document the clean-machine integration test sequence and the migration note for operators with existing `OPENCLAW_AGENT_WORKSPACE` in their `.env`.

### 2.7 Populate `plan.2026-04-20T09-21.md`

File: `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/plan.2026-04-20T09-21.md`

The plan currently contains only boilerplate phase headings with no tasks. It must be populated with concrete tasks per the `atomic-plan-contract`. At minimum, each phase needs:

- **Phase 0**: link the spec, record the current branch/commit baseline, list required fixtures (running Docker Desktop, upstream image available, Anthropic API key for onboarding test).
- **Phase 1**: confirm spec is locked (section 2.6 above must be done first); sync to branch; confirm Pester and PSScriptAnalyzer are available.
- **Phase 2**: add Pester regression test(s) that fail before the fix (a test that calls the validation script against a mocked env with an empty `OPENCLAW_GATEWAY_TOKEN` and expects `IsExpected = $false`).
- **Phase 3**: implement fixes for 2.2 through 2.5 above; add onboarding script (2.1).
- **Phase 4**: run formatter → PSScriptAnalyzer → Pester; record baseline, post-change, and comparison artifacts per `evidence-and-timestamp-conventions`.
- **Phase 5**: update spec/issue with outcomes.
- **Phase 6**: PR notes.
- **Phase 7**: rollout notes and migration guidance.

### 2.8 Update `AGENTS.md`

`AGENTS.md` is auto-generated from `/.github/instructions/*.instructions.md` files by `scripts/dev-tools/sync-agents-from-instructions.ps1`. If the service topology needs to be reflected in `AGENTS.md`, the correct path is to add a PowerShell instructions file that describes `openclaw-agent` as a required service and regenerate. Direct edits to `AGENTS.md` would be overwritten. This is a design question for the owner; at minimum, the gap must be documented.

### 2.9 Audit issue #30 feature folder for stale "optional" language

Directory: `docs/features/active/2026-04-16-openclaw-agent-docker-30/`

The v1 and v2 spec files still contain `8181` port references and describe the agent as an additive service. These files are archived artifacts from the prior feature. The owner must decide whether they are canonical (and need updating) or archival (and should be moved to a `docs/features/closed/` path). The research does not make this decision; it flags it as an open question.

### 2.10 Evidence artifacts required by fail-closed rule

Per the plan's fail-closed evidence rule, the following artifact paths must be produced and recorded before the plan can be marked complete:

- `artifacts/evidence/<timestamp>-baseline-pester-coverage.md` — baseline Pester coverage for `scripts/Invoke-OpenClawContainerPathValidation.ps1` before changes.
- `artifacts/evidence/<timestamp>-post-change-pester-coverage.md` — post-change coverage after extensions are added.
- `artifacts/evidence/<timestamp>-coverage-comparison.md` — comparison confirming coverage at or above 90% for the changed script.

---

## 3. Upstream-Onboarding Verification

### 3.1 Findings from `docs.openclaw.ai`

The upstream Docker documentation was accessible. The following are confirmed via `WebFetch` against `https://docs.openclaw.ai/install/docker.md` and `https://docs.openclaw.ai/start/wizard-cli-automation.md`.

**Onboarding command (manual flow):**

```bash
docker compose run --rm --no-deps --entrypoint node openclaw-gateway \
  dist/index.js onboard --mode local --no-install-daemon
```

The service name in the upstream compose file is `openclaw-gateway`. In this repository the service is named `openclaw-agent`. The adapter must therefore invoke the command against `openclaw-agent`.

**Gateway token variable name:**

The upstream documentation explicitly names the variable `OPENCLAW_GATEWAY_TOKEN`. Confirmed verbatim: "the setup script automatically generates `OPENCLAW_GATEWAY_TOKEN` and writes it to `.env`." This variable name matches what is already in `docker-compose.yml` line 75 and `.env.example`. The variable name itself is correct in the staged changes; only the value (a placeholder rather than a generated token) is wrong.

**Provider API key flags for non-interactive mode:**

The wizard CLI automation documentation confirms `--non-interactive` suppresses all prompts. Anthropic key:

```bash
openclaw onboard --non-interactive \
  --mode local \
  --auth-choice apiKey \
  --anthropic-api-key "$ANTHROPIC_API_KEY" \
  --secret-input-mode plaintext \
  --gateway-port 18789 \
  --gateway-bind loopback \
  --install-daemon \
  --daemon-runtime node \
  --skip-skills
```

**Note:** `--install-daemon` and `--skip-skills` are appropriate for a Docker-only deployment. The PowerShell onboarding script should pass `--no-install-daemon` for this repository's Docker-managed deployment (matching the upstream manual Docker flow), not `--install-daemon`.

**`--allow-unconfigured` behavior:**

The upstream documentation does not document `--allow-unconfigured` on any page fetched. The flag is present in the staged Dockerfile CMD but has no upstream documentation. The most consistent interpretation — supported by the fact that `openclaw.json` still has `auth.mode: "token"` — is that `--allow-unconfigured` allows the gateway process to start without completing onboarding, but it does not override `auth.mode`. If `auth.mode` is `"token"` in the loaded config, the dashboard will still prompt for the token. This hypothesis is consistent with the observed bug (gateway starts healthy, dashboard prompts for credential).

**Separate onboarding entrypoint:**

Confirmed: the upstream flow uses `--entrypoint node` with `dist/index.js onboard` as a pre-start "run once" container invocation (not `docker compose up`). The gateway entrypoint (`docker-entrypoint.sh`) is the runtime entrypoint. The onboarding command overrides the entrypoint to run the onboard subcommand directly, then exits; the gateway is started separately via `docker compose up`.

**Binary path conflict:**

The upstream manual command uses `dist/index.js`. The staged Dockerfile CMD uses `openclaw.mjs`. This discrepancy is unresolved. It may reflect a version difference between the upstream documentation and the actual image content, or the image may expose both. The onboarding script must verify which path is correct by running a test invocation before assuming `dist/index.js` works in the `${OPENCLAW_AGENT_IMAGE}` image that this repository uses.

---

## 4. Behavior Semantics and Edge Cases

### 4.1 Entrypoint copy semantics

`deploy/docker/openclaw-agent-entrypoint.sh` unconditionally `cp`s all seed files from `/opt/openclaw-assistant-seed/` into `/workspace` and `/.openclaw` on every container start. If the upstream onboarding step writes files into `/workspace` (e.g., an onboarding state file or a populated `openclaw.json` with the gateway token already inlined), those writes will be **overwritten on every restart** because the copy is unconditional. This is a destructive behavior for any state produced by onboarding.

Specifically: `cp "$seed_dir/openclaw.json" "$workspace_dir/openclaw.json"` on every start means the `auth.token` field set during onboarding would be reset to the seed file value (which contains only `"mode": "token"` with no actual token). This must be changed to a conditional copy (copy only if the target does not exist, or skip fields managed by onboarding).

### 4.2 Restart behavior with compose `build:`

The `openclaw-agent` service now uses `build:` instead of `image:`. Running `docker compose up -d` does **not** trigger a rebuild unless `--build` is passed or the image does not exist. Operators who edit `deploy/docker/openclaw-assistant/openclaw.json` or other seed files will not see changes take effect until they run `docker compose up --build`. The runbook step 3 currently passes `--build`, so this is correct for the initial start; it must be documented for subsequent updates.

### 4.3 Migration for operators with `OPENCLAW_AGENT_WORKSPACE` in `.env`

The `OPENCLAW_AGENT_WORKSPACE` env var has been removed from `.env.example` and from the compose volume definition. Operators who copied `.env.example` at issue #30's merge and have `OPENCLAW_AGENT_WORKSPACE=./deploy/docker/openclaw-assistant` in their `.env` will see this variable silently ignored by compose. Docker will not fail; the variable is simply unused. A migration note should be added to the runbook explaining that this variable is no longer needed and can be removed from `.env`.

### 4.4 `docker-entrypoint.sh` upstream exec risk

`openclaw-agent-entrypoint.sh` concludes with `exec docker-entrypoint.sh "$@"`. If the upstream image (`${OPENCLAW_AGENT_IMAGE}`) does not provide a binary named `docker-entrypoint.sh` on `$PATH`, the wrapper will fail with a silent "not found" error at startup. The upstream image identity (`ghcr.io/openclaw/openclaw:latest`) is set at build time, not validated. If upstream changes the entrypoint name, the wrapper breaks. This risk should be mitigated by validating the entrypoint path during the build stage or accepting a known-good upstream image pin.

---

## 5. Requirements Mapping

Each "Expected Behavior" bullet from `issue.md` is mapped to the concrete artifact that must exist or be created to satisfy it.

| Expected behavior | Mapped artifact | Gap? |
|---|---|---|
| Operators reach a usable agent UI without an undocumented authentication wall | `scripts/Invoke-OpenClawAgentOnboarding.ps1` generates token; `openclaw.json` references it; runbook documents the flow | **Gap — script does not exist** |
| Runbook presents agent as integral, not optional | `docs/mailbridge-runbook.md` "Optional OpenClaw Assistant Service" heading renamed; "optional" language removed from purpose block | **Gap — heading and purpose block unchanged** |
| Single scripted diagnostic returns pass/fail covering container health, HostAdapter reachability, gateway readiness, and dashboard auth state | `scripts/Invoke-OpenClawContainerPathValidation.ps1` with all five sub-checks implemented | **Partial gap — `/readyz`, in-container probe, token check, and auth probe missing** |
| All runbook port references match compose (`18789`, `8080`, `4319`) | Compose confirms `18789` and `8080`; runbook live text no longer references `8181` | Pass for runbook; `8081` default in validation script is a bug |
| Gateway token produced by onboarding step, written to `.env`, operator knows where to find it | `scripts/Invoke-OpenClawAgentOnboarding.ps1`; `.env.example` placeholder with comment | **Gap — script does not exist; current `.env` has hard-coded placeholder** |

---

## 6. Testing Implications

### 6.1 Pester strategy for `Invoke-OpenClawContainerPathValidation.ps1`

**Seams to mock:**
- `Invoke-WebRequest` — already mocked in existing tests via `Mock Invoke-WebRequest`.
- Docker CLI calls — already mocked via `Invoke-FakeDocker` function injection.
- File system reads for `.env` content — use `Mock Get-Content` or inject the file path as a parameter so tests can pass a synthetic `.env` content.
- `docker compose exec` calls — must be routed through `Invoke-OpenClawDockerCommand` (the existing wrapper function) so they can be mocked with the same `Invoke-FakeDocker` pattern.

**Pass/fail branch matrix (additional tests needed):**
- `OPENCLAW_GATEWAY_TOKEN` present and non-empty → check passes.
- `OPENCLAW_GATEWAY_TOKEN` absent from `.env` → check fails.
- `OPENCLAW_GATEWAY_TOKEN` present but empty → check fails.
- `/readyz` returns 200 → check passes.
- `/readyz` returns 503 → check fails and sets `OverallResult = Unexpected`.
- In-container HostAdapter probe (`docker compose exec`) returns 200 → check passes.
- In-container probe returns non-200 or fails → check fails.
- Auth probe with correct token returns 200 → check passes.
- Auth probe with wrong token returns 401 → check fails.

**90% coverage assessment:** The existing five test cases cover the main happy path, the degraded-ready path, the missing-container path, the exception/throw path, and JSON output. The new checks (four additional functions) will need at least one positive and one negative test each to maintain 90% coverage on new code. That is approximately four additional test cases at minimum. The test file is already 238 lines; ensure it stays under 500 lines after additions.

**Current `8081` test hardcoding bug:** all existing tests pass `CoreBaseUrl` implicitly using the default `8081`. Once the default is corrected to `8080`, the mock URIs in all five existing tests must be updated to `8080`; otherwise the tests will fail because the mock switch does not match the new URI. This is a required test change, not optional.

### 6.2 Manual and integration validation sequence

For a clean-machine integration test:

1. Run `docker compose down -v` to remove all volumes including `openclaw_agent_workspace`.
2. Run `docker rmi openclaw/agent:pre-mvp` to force a fresh image build.
3. Edit `.env` so `OPENCLAW_GATEWAY_TOKEN=` is empty.
4. Run `scripts/Invoke-OpenClawAgentOnboarding.ps1` with a real Anthropic API key.
5. Confirm `.env` now contains a non-empty, non-placeholder `OPENCLAW_GATEWAY_TOKEN`.
6. Run `docker compose up --build -d openclaw-core openclaw-agent`.
7. Run `scripts/Invoke-OpenClawContainerPathValidation.ps1`.
8. Confirm `OverallResult: Expected` with all five sub-checks passing.
9. Open `http://127.0.0.1:18789/` in a browser; confirm the dashboard connects using the generated token without prompting for credentials or manually editing any file.
10. Record baseline, post-change, and comparison evidence artifacts under `artifacts/evidence/` per `evidence-and-timestamp-conventions`.

---

## Appendix: Rejected Alternative Approaches

**Set `auth.mode: "none"` in `openclaw.json`** — rejected because it contradicts the upstream onboarding contract: the upstream flow specifically generates and persists a token. Removing auth entirely is a security regression that removes the only credential gate on the local dashboard. The issue owner's framing ("the token that is generated in this process is what I seem to be lacking") confirms the intent is to produce a real token, not remove auth.

**Use the `openclaw-dev-token` hard-coded default** — rejected because it is not a generated credential; it is a well-known placeholder that any local attacker can reuse, and it does not solve the operator's inability to know what to paste into the dashboard.
