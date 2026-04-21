---
name: Issue 38 update mirror
description: Local mirror of the intended GitHub issue update for issue #38 upon PR opening.
type: evidence
---

Timestamp: 2026-04-20T09-21

Intended text (post as a comment on https://github.com/drmoisan/open-claw-bridge/issues/38):

---

Implementation is complete on branch `bug/cannot-access-agent-in-docker-38`. PR notes at `docs/features/active/2026-04-20-cannot-access-agent-in-docker-38/pr-notes.md`.

**What changed**

1. New `scripts/Invoke-OpenClawAgentOnboarding.ps1` runs the upstream OpenClaw onboard flow and writes the generated `OPENCLAW_GATEWAY_TOKEN` to `.env`.
2. `scripts/Invoke-OpenClawContainerPathValidation.ps1` extended with four probes (`AgentReadyz`, `HostAdapterInContainer`, `GatewayTokenPresence`, `DashboardAuth`) and now returns a single `OverallResult` covering the container path end to end.
3. `docker-compose.yml` no longer ships a `openclaw-dev-token` default (breaking change, documented in PR notes).
4. `deploy/docker/openclaw-agent-entrypoint.sh` preserves onboarding state across restarts.
5. Documentation reframes `openclaw-agent` as required in `README.md`, `docs/mailbridge-runbook.md`, `docs/architecture-diagrams.md`, and `AGENTS.md`.
6. Port references normalized to `${OPENCLAW_AGENT_PORT:-18789}`, `${OPENCLAW_HTTP_PORT:-8080}`, and `4319`.

**Validation**

- 97 Pester tests passed; 0 failed.
- Repository-wide coverage 86.97% (baseline 81.71%, +5.26 pp).
- New/extended files exceed the 90% new-code threshold (validation 90.28%, onboarding 98.55%, module 94.63%).
- 14 of 15 ACs PASS; AC-15 is the clean-machine integration gate, which is a manual verification step documented in the PR notes.

**Evidence root**: `artifacts/evidence/2026-04-20T09-21-issue-38/`.

---

PostedAs: unknown (local mirror only; posting handled by the orchestrator at PR time)
IssueURL: https://github.com/drmoisan/open-claw-bridge/issues/38
