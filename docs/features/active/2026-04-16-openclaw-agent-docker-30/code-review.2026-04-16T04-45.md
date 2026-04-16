# Code Review: openclaw-agent-docker (#30)

---

**Review Date:** 2026-04-16
**Reviewer:** GitHub Copilot (feature_code_review_agent)
**Feature Folder:** `docs/features/active/2026-04-16-openclaw-agent-docker-30/`
**Feature Folder Selection Rule:** Matched by issue number suffix (`-30`) in the feature folder name.
**Base Branch:** `development`
**Head Branch:** `feature/openclaw-agent-docker-30`
**Review Type:** Initial review

---

## Executive Summary

This change adds an external OpenClaw AI assistant runtime (`openclaw-agent`) as a new Docker Compose service alongside the existing `openclaw-core` service. The implementation scope is limited to Docker Compose definitions (2 files), environment configuration (1 file), assistant instruction and tool definition files (3 new files), and documentation updates (3 files). No C# or other programmatic code was changed.

**What changed:**
- `docker-compose.yml` gained a new `openclaw-agent` service with the same security posture as `openclaw-core` (loopback-only ports, non-root user, read-only root filesystem, `cap_drop: ALL`, `no-new-privileges`).
- `docker-compose.dev.yml` gained a corresponding dev override with `extra_hosts` for Linux compatibility.
- `.env.example` added three new variables: `OPENCLAW_AGENT_IMAGE`, `OPENCLAW_AGENT_PORT`, `OPENCLAW_AGENT_WORKSPACE`.
- Three new files under `deploy/docker/openclaw-assistant/` define the assistant's HTTP-based tool definitions, system behavioral constraints, and a placeholder configuration template.
- `README.md`, `docs/architecture-diagrams.md`, and `docs/mailbridge-runbook.md` were updated with service documentation.

**Top 3 risks:**
1. `OPENCLAW_AGENT_IMAGE` is unverified — the external OpenClaw platform documentation was unreachable during research, so the exact image reference and config schema are placeholders.
2. Dev override in `docker-compose.dev.yml` re-declares `ports` and `volumes` that already exist in the base compose file, introducing redundancy that may cause confusion during future maintenance.
3. No healthcheck is defined for `openclaw-agent`, which means Docker Compose cannot report meaningful health status for this service.

**PR readiness recommendation:** **Conditional Go** — The implementation is structurally complete and meets all acceptance criteria. The known risks (unverified image, dev override redundancy, missing healthcheck) are documented and do not block merge, but items 2 and 3 are recommended for follow-up.

---

## Findings Table

| Severity | File | Location | Finding | Recommendation | Rationale | Evidence |
|---|---|---|---|---|---|---|
| Minor | `docker-compose.dev.yml` | Lines 35-44 | Dev override re-declares `ports` and `volumes` that are already defined in the base `docker-compose.yml` for `openclaw-agent`. Docker Compose merges these additively for volumes and replaces for ports, producing the correct result, but the duplication is unnecessary. | Remove the `ports` and `volumes` blocks from the dev override and keep only the `extra_hosts` key, which is the actual dev-specific addition. | Redundant declarations increase maintenance burden and may cause confusion when the base compose values are updated but the dev override is not. | Diff inspection: base compose lines 72-81 define identical port and volume entries; dev override lines 35-44 repeat them. |
| Minor | `docker-compose.yml` | Lines 55-81 | No `healthcheck` defined for `openclaw-agent`. The existing `openclaw-core` service defines a healthcheck, but the agent service does not. | Add a healthcheck once the external image's health endpoint is verified, or document explicitly in the runbook that the agent service does not support health monitoring via compose. | Without a healthcheck, `docker compose ps` cannot report `healthy`/`unhealthy` status for the agent, limiting operator visibility. The spec acknowledges this as contingent on image capabilities. | Diff inspection: `openclaw-core` has `healthcheck` block at lines 47-52; `openclaw-agent` has none. |
| Minor | `spec.md` | Configuration files table | Spec references `deploy/agent-workspace/TOOLS.md` and `deploy/agent-workspace/INSTRUCTIONS.md` as new file paths. The implementation uses `deploy/docker/openclaw-assistant/TOOLS.md` and `deploy/docker/openclaw-assistant/SYSTEM.md`. | Update the spec to reflect the actual implemented paths and file names. | Inconsistency between spec and implementation may cause confusion during future audits or handoffs. | Spec configuration files table vs. actual file paths in the working tree. |
| Info | `.env.example` | Line 18 | `OPENCLAW_AGENT_IMAGE` is set to a placeholder value (`<placeholder — verify at docs.openclaw.ai before use>`) that will cause `docker compose up` to fail if not replaced. | This is intentional by design and documented in the README. No action required. | Documented behavior: README section "4. Optional" explicitly warns that the placeholder is not a valid image reference. | `.env.example` line 18; `README.md` assistant section final note. |
| Info | `deploy/docker/openclaw-assistant/config.yaml` | Full file | The configuration template contains only placeholder values and no schema validation. | Consider adding a JSON Schema or YAML schema reference once the official configuration schema is verified against OpenClaw platform documentation. | Currently acceptable as a placeholder; the warning comment at the top of the file communicates this clearly. | File inspection: all values are `<PLACEHOLDER — ...>`. |

No Blockers or Major findings.

---

## Implementation Audit

### Docker Compose implementation audit

#### What changed well

- The `openclaw-agent` service definition closely mirrors the security posture of `openclaw-core`: identical `user`, `read_only`, `cap_drop`, `security_opt`, and loopback-only `ports` configuration. This demonstrates adherence to the principle of least privilege and the existing security conventions.
- The service is fully independent from `openclaw-core` with no `depends_on` relationship, allowing either service to start, stop, or fail without affecting the other.
- Environment variable parameterization uses the same patterns as the existing stack (`${VAR:-default}` syntax), maintaining consistency.
- The `tmpfs` mount uses a smaller size (`64m` vs `256m`) appropriate for the assistant's anticipated workload.

#### Configuration and service topology

- Both services independently consume the HostAdapter API via `host.docker.internal:4319`, which is consistent with the architecture diagram.
- The token file bind mount is correctly configured as read-only in both the base and dev compose files.
- The `init: true` and `restart: unless-stopped` settings are appropriate for a long-running container service.

#### Error handling and operational safety

- The placeholder `OPENCLAW_AGENT_IMAGE` value will produce a clear Docker failure (invalid image reference) rather than silently failing, which is the correct fail-fast behavior.
- Documentation includes a troubleshooting table in the runbook covering the most likely operational issues (401 Unauthorized, container exit on startup, host.docker.internal resolution failure).

### Documentation audit

#### What changed well

- The naming distinction between `OpenClaw.Core` (repo-owned UI/cache container) and `openclaw-agent` (external assistant runtime) is consistently maintained across all documentation updates.
- The README provides a complete operational workflow: start, check status, view logs, stop, and validate connectivity.
- The architecture diagram update correctly shows both services consuming the HostAdapter independently.
- The runbook adds structured troubleshooting guidance with a clear symptom/cause/action table.

#### Documentation quality

- All documentation uses concrete command examples with correct syntax.
- The runbook correctly notes that stopping one service does not affect the other.
- The connectivity verification command in the README demonstrates authenticated access to the assistant port.

### Assistant instruction files audit

#### Tool definitions (`TOOLS.md`)

- All six HostAdapter endpoints are defined as HTTP-based tools, each with explicit endpoint, method, headers, parameters, expected response, and error response sections.
- No references to CLI exec or `OpenClaw.MailBridge.Client.exe` appear anywhere in the file.
- Authentication is consistently described as reading the token from `/run/openclaw/hostadapter.token`.

#### System instructions (`SYSTEM.md`)

- Five behavioral constraints are defined: read-only operation, no-write claims, redaction awareness, human-approval gating, and safe-mode-first.
- The read-only constraint explicitly prohibits writing, sending, replying, forwarding, modifying, moving, or deleting data.
- The no-write-claims constraint prohibits language that implies completed write actions.
- The redaction awareness section correctly instructs the assistant to surface redacted fields rather than hallucinating content.

---

## Test Quality Audit

No unit tests or integration tests were added or modified. This is appropriate because:

- No programmatic code was changed.
- Docker Compose validation was performed via `docker compose config` (evidence: `evidence/qa-gates/qa-compose-config.md`, `evidence/qa-gates/qa-compose-dev-config.md`).
- Regression testing was performed via baseline comparison of the `openclaw-core` service definition (evidence: `evidence/qa-gates/qa-core-regression.md`).

### Reviewed test and QA artifacts

- `evidence/qa-gates/qa-compose-config.md` — Production compose config validation, EXIT_CODE: 0, security posture properties verified for `openclaw-agent`
- `evidence/qa-gates/qa-compose-dev-config.md` — Dev combined compose config validation, EXIT_CODE: 0, three services validated
- `evidence/qa-gates/qa-core-regression.md` — Line-by-line comparison confirming `openclaw-core` service block is semantically identical to baseline
- `evidence/qa-gates/qa-acceptance-criteria.md` — All 10 acceptance criteria evaluated as PASS with file-level evidence

### Quality assessment prompts

- **Determinism:** QA evidence is based on deterministic `docker compose config` output and diff comparison. No flaky dependencies.
- **Isolation:** Each QA gate artifact validates a single property (config validity, security posture, core regression, AC status).
- **Speed:** Not applicable to unit test speed; compose config validation is fast.
- **Diagnostics:** Evidence artifacts include command, exit code, and output summary, providing clear diagnostic context.

---

## Security / Correctness Checks

| Check | Status | Evidence |
|---|---|---|
| No secrets in code | ✅ PASS | Token is read from a bind-mounted file at runtime; no secrets appear in compose files, environment variables, or image layers. `.env.example` contains only placeholder values. |
| No unsafe subprocess or command construction | ✅ PASS | No subprocess execution. All tool definitions use HTTP requests, not CLI exec. |
| Input validation at boundaries | ✅ PASS | HostAdapter validates bearer tokens and request parameters. The assistant does not introduce new input boundaries. |
| Error handling remains explicit | ✅ PASS | Docker Compose will fail fast on invalid `OPENCLAW_AGENT_IMAGE`. Token authentication failures return explicit 401 responses from HostAdapter. |
| Configuration / path handling is safe | ✅ PASS | All paths use parameterized defaults. Token mount is read-only. Root filesystem is read-only. `cap_drop: ALL` and `no-new-privileges` limit container capabilities. |
| Loopback-only network exposure | ✅ PASS | Port publishing uses `127.0.0.1:${OPENCLAW_AGENT_PORT:-8181}:8181`, consistent with the existing `openclaw-core` pattern. No external network exposure. |
| Non-root container execution | ✅ PASS | `user: "1654:1654"` matches the existing `openclaw-core` non-root user convention. |

---

## Research Log

No external research was required for this review. All evidence was sourced from the diff, feature folder artifacts, and QA gate evidence files within the repository.

---

## Verdict

The implementation is structurally complete and meets all 10 acceptance criteria. The Docker Compose definitions follow the existing security conventions, the assistant instruction files enforce appropriate behavioral constraints, and the documentation updates are comprehensive. The three Minor findings (dev override redundancy, missing healthcheck, spec path discrepancy) do not block merge but are recommended for follow-up cleanup. The `OPENCLAW_AGENT_IMAGE` placeholder is an acknowledged prerequisite, not a defect.

**Recommendation: Conditional Go** — ready for merge with a follow-up to clean up the dev override redundancy and update the spec file paths. The healthcheck gap is deferred pending verification of the external image's capabilities.
