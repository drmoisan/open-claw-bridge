# cannot-access-agent-in-docker (Issue #38)

- Date captured: 2026-04-20
- Author: drmoisan
- Status: Promoted -> docs/features/active/cannot-access-agent-in-docker/ (Issue #38)

> Automation note: Keep the section headings below unchanged; the promotion tooling maps each of them into the GitHub bug issue template.

- Issue: #38
- Issue URL: https://github.com/drmoisan/open-claw-bridge/issues/38
- Last Updated: 2026-04-20
- Work Mode: full-bug

## Summary

The OpenClaw agent is treated as an optional add-on across the runbook, compose stack, and diagnostic steps, yet it is the core end-user surface of the solution. When operators follow the current runbook, the Gateway Dashboard at `http://127.0.0.1:18789/` prompts for a shared-secret credential that no documented step produces, because the mandatory OpenClaw onboarding stage (which generates and persists the gateway token to `.env`) is skipped. Operators cannot reach the agent UI even when every container reports healthy. Port references in the runbook (`http://127.0.0.1:8181/`) also disagree with the actual published port (`18789`) in [docker-compose.yml:71](docker-compose.yml#L71), and the current verification procedure is a loose collection of one-off `curl`/`docker compose exec` calls with no single pass/fail script.

## Environment

- OS/version: Windows 11 Pro 10.0.26200 (operator workstation)
- Python version: not applicable — stack is .NET 10 on Windows host plus Docker Desktop containers
- Command/flags used:
  - `docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up -d openclaw-agent`
  - Browser navigation to `http://127.0.0.1:18789/`
  - Per-runbook verification: `curl.exe http://127.0.0.1:8181/` (from [docs/mailbridge-runbook.md:498](docs/mailbridge-runbook.md#L498))
- Data source or fixture: `OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest` (per [README.md:273](README.md#L273)); HostAdapter token at `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`; assistant workspace `./deploy/docker/openclaw-assistant/` with the unverified placeholder [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) whose `_placeholder` key explicitly warns that values are unverified.

## Steps to Reproduce

1. Follow the runbook section "Optional OpenClaw Assistant Service" in [docs/mailbridge-runbook.md:467-513](docs/mailbridge-runbook.md#L467-L513). Set `OPENCLAW_AGENT_IMAGE=ghcr.io/openclaw/openclaw:latest` in `.env`.
2. Run `docker compose --env-file .env -f .\docker-compose.yml -f .\docker-compose.dev.yml up -d openclaw-agent`.
3. Wait for `docker ps` to report `openclaw-agent` as healthy.
4. Open `http://127.0.0.1:18789/` in a browser (the actual published port from [docker-compose.yml:71](docker-compose.yml#L71), not the `8181` the runbook quotes at [docs/mailbridge-runbook.md:498](docs/mailbridge-runbook.md#L498)).
5. Observe the Gateway Dashboard credential prompt (see screenshot below).
6. Attempt to authenticate: no shared-secret or password has been generated or written to `.env` by any runbook step, so there is no credential to paste.

## Expected Behavior

- Operators who complete the documented install path reach a usable agent UI without an undocumented authentication wall.
- The runbook presents the agent as an integral component of the solution, not an optional overlay, because no supported workflow exists without it.
- A single scripted diagnostic returns a pass/fail result covering container health, HostAdapter reachability from inside the container, gateway readiness (`/readyz`), and the dashboard authentication state.
- All runbook port references match the published ports in [docker-compose.yml](docker-compose.yml) (`18789` for the agent; `8080` for `openclaw-core`; `4319` for the HostAdapter).
- Documented environment configuration matches the official OpenClaw onboarding flow described at [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker): the gateway token used by `gateway.auth.mode=token` in [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) is produced by a one-time onboarding step and written to `.env`, and the operator knows exactly where to find and paste that token.

## Actual Behavior

- The runbook section is titled "Optional OpenClaw Assistant Service" ([docs/mailbridge-runbook.md:467](docs/mailbridge-runbook.md#L467)) and its text says the service "sits beside `openclaw-core` as a separate consumer" of the HostAdapter. There is no supported end-user workflow without the agent, so the "optional" framing is incorrect.
- The runbook verification step uses `http://127.0.0.1:8181/` ([docs/mailbridge-runbook.md:498](docs/mailbridge-runbook.md#L498)), but the compose file publishes `127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}:18789` ([docker-compose.yml:71](docker-compose.yml#L71)). The v1 spec used `:8181`; the v2 spec and shipped compose file use `:18789`. The runbook still references the obsolete `8181` value.
- Diagnostics are split across several ad-hoc commands in [docs/mailbridge-runbook.md:494-506](docs/mailbridge-runbook.md#L494-L506) (one `curl` on the host, one `docker compose exec` inside the container, no aggregated result). There is no single `verify-openclaw-agent.ps1` or equivalent that returns an overall pass/fail.
- The Gateway Dashboard at `http://127.0.0.1:18789/` prompts for a shared secret. The current deployment ships [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) with `gateway.auth.mode: "token"` but does not perform the onboarding step that the official OpenClaw Docker install documents. Per [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker), setup is expected to "prompt for provider API keys" and "generate a gateway token and write it to `.env`" before the container starts; the repository's install path starts the container directly from `OPENCLAW_AGENT_IMAGE` and never runs `openclaw onboard`, so no gateway token exists for the operator to paste into Settings.

## Logs / Screenshots

- [x] Attached minimal logs or screenshot
- Snippet:

```text
Browser: http://127.0.0.1:18789/
Dashboard prompt: "Enter shared secret"  (no credential has been issued to the operator)

Docker status (representative):
  openclaw-agent   healthy
  openclaw-core    healthy

docker compose exec openclaw-agent sh -c 'curl -H "Authorization: Bearer $(cat /run/openclaw/hostadapter.token)" http://host.docker.internal:4319/v1/status'
  -> HTTP 200 (HostAdapter reachable; bridge is not the problem)

curl.exe http://127.0.0.1:18789/readyz
  -> HTTP 200 (gateway process is ready; only the dashboard auth path is blocked)
```

- Screenshot referenced in the original request: Gateway Dashboard credential prompt with no documented source for the expected shared secret.

## Impact / Severity

- [x] Blocker
- [ ] High
- [ ] Medium
- [ ] Low

Rationale: the agent is the primary end-user surface. An operator who follows the published runbook end-to-end cannot authenticate to the dashboard and therefore cannot use the solution at all.

## Suspected Cause / Notes

Likely contributing factors, for the implementer to confirm:

1. **Deployment skipped the OpenClaw onboarding step.** The repository's install path pulls `ghcr.io/openclaw/openclaw:latest` and starts it directly via `docker compose up -d openclaw-agent`, but the official Docker install at [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker) specifies a first-run `setup.sh` / `openclaw onboard` step that generates the gateway shared secret and persists it into `.env`. Without that step, the container starts with `gateway.auth.mode=token` but the operator has no token to present.
2. **Agent was modelled as optional from the start.** [docs/features/potential/2026-04-16-openclaw-agent-docker.md](docs/features/potential/2026-04-16-openclaw-agent-docker.md) and the v1/v2 specs under [docs/features/active/2026-04-16-openclaw-agent-docker-30/](docs/features/active/2026-04-16-openclaw-agent-docker-30/) describe the agent as an additive service. The delivered runbook inherits that framing even though operator-facing reality is the opposite.
3. **Port drift between v1 and v2 of the feature.** The v1 spec used port `8181`; v2 and the shipped compose file moved to `18789`. The runbook verification block still references `8181`.
4. **`openclaw.json` is explicitly a placeholder.** Line 2 of [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) warns that all values must be verified against `docs.openclaw.ai/gateway/configuration-reference` before production use. The gateway-auth stanza was never reconciled with the real onboarding behavior.

Files to inspect:

- [docs/mailbridge-runbook.md](docs/mailbridge-runbook.md) — sections "Install Path C" and "Optional OpenClaw Assistant Service"
- [docker-compose.yml](docker-compose.yml) and [docker-compose.dev.yml](docker-compose.dev.yml) — `openclaw-agent` service definition and published ports
- [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) — gateway auth configuration
- [README.md:267-310](README.md#L267-L310) — assistant image guidance
- [scripts/](scripts/) — missing `verify-openclaw-agent.ps1` and missing `onboard-openclaw-agent.ps1`
- [.env.example](.env.example) — missing `OPENCLAW_GATEWAY_TOKEN` (or equivalent) and any onboarding-derived keys

## Proposed Fix / Validation Ideas

The fix has four work streams. Each should be validated against the upstream install doc at [docs.openclaw.ai/install/docker](https://docs.openclaw.ai/install/docker#docker) before landing.

1. **Reframe the agent as integral, not optional.**
   - Rename the runbook section "Optional OpenClaw Assistant Service" to "OpenClaw Agent (Required)" and fold it into the primary install path rather than an appendix.
   - Update [README.md](README.md), [docs/architecture-diagrams.md](docs/architecture-diagrams.md), and [AGENTS.md](AGENTS.md) to describe `openclaw-agent` as a required service in every deployment.
   - Remove the "Install Path C is optional" framing in [docs/mailbridge-runbook.md:361-465](docs/mailbridge-runbook.md#L361-L465); keep the Windows bridge + HostAdapter + agent as a single install path.

2. **Add a scripted agent onboarding step that matches the upstream setup flow.**
   - Add `scripts/onboard-openclaw-agent.ps1` that reproduces what `setup.sh` does upstream: run the agent image's `onboard` entrypoint once, capture the generated gateway token, and write it to `.env` under the upstream-sanctioned variable name (likely `OPENCLAW_GATEWAY_TOKEN` — confirm from upstream before committing).
   - Add `OPENCLAW_GATEWAY_TOKEN=` (empty placeholder) to `.env.example` with a comment pointing operators at the onboarding script.
   - Document how to rotate the token and how to choose password-auth instead of token-auth if the operator prefers.
   - Update [deploy/docker/openclaw-assistant/openclaw.json](deploy/docker/openclaw-assistant/openclaw.json) so `gateway.auth` is sourced consistently with whatever the onboarding step produces; remove the `_placeholder` warning once values are verified.

3. **Replace the clunky per-service diagnostic with a single verification script.**
   - Add `scripts/verify-openclaw-agent.ps1` that performs, in order, and returns a single pass/fail:
     1. Confirm `docker compose ps openclaw-agent` reports `healthy`.
     2. Confirm `GET http://127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}/readyz` returns `200`.
     3. From inside the container, confirm `GET http://host.docker.internal:4319/v1/status` with the bind-mounted bearer token returns `200`.
     4. Confirm the dashboard shared-secret variable (`OPENCLAW_GATEWAY_TOKEN` or equivalent) is present in `.env` and non-empty.
     5. Confirm the dashboard accepts that shared secret programmatically (e.g., a POST against the auth endpoint), so the operator is not left to discover auth failure interactively.
   - Replace the ad-hoc `curl`/`docker compose exec` block in [docs/mailbridge-runbook.md:494-506](docs/mailbridge-runbook.md#L494-L506) with a single invocation of the verification script.

4. **Correct the port references and align documentation with the compose stack.**
   - Update every runbook/README reference that still says `8181` to `${OPENCLAW_AGENT_PORT:-18789}`.
   - Audit [docs/mailbridge-runbook.md:413-456](docs/mailbridge-runbook.md#L413-L456) for any other stale port values (HostAdapter `4319`, core `8080` look correct but should be re-verified against [docker-compose.yml](docker-compose.yml)).

Validation ideas:

- [ ] Unit coverage areas: helper functions added to `scripts/` should be covered by Pester tests under `tests/` that stub Docker CLI calls and validate pass/fail branching.
- [ ] Integration scenario to retest: clean workstation, fresh clone, follow runbook end-to-end, reach a working agent dashboard without manual edits to `.env` or `openclaw.json`.
- [ ] Manual verification notes: on a machine with no prior OpenClaw state, confirm that (a) `onboard-openclaw-agent.ps1` generates a token and writes it to `.env`, (b) `verify-openclaw-agent.ps1` returns pass, and (c) pasting the recorded shared secret into the dashboard Settings grants access. Record evidence under `artifacts/` per the repository's evidence-and-timestamp conventions.

## Next Step

- [ ] Promote to GitHub issue (bug-report template)
- [ ] Move to active fix folder / branch