---
Timestamp: 2026-04-21T14-00
Purpose: Baseline Docker agent container state prior to Phase 4 Dockerfile edit (AC-1 pre-fix anchor)
---

# Baseline — Agent Container State

Timestamp: 2026-04-21T14-00

Command: pwsh -Command "docker compose logs --tail=200 openclaw-agent 2>&1"

EXIT_CODE: 0

Output Summary:
- Agent container is running with the pre-fix image.
- Baseline gateway ready line (5 plugins, no ACP): `[gateway] ready (5 plugins: acpx, browser, device-pair, phone-control, talk-voice; 4.8s)` at `2026-04-21T13:41:00.787+00:00`.
- Pre-fix embedded ACP runtime registration line: `[plugins] embedded acpx runtime backend registered (cwd: /workspace)` at `2026-04-21T13:41:03.447+00:00`.
- Pre-fix failure line captured (AC-1 target to eliminate): `[plugins] embedded acpx runtime backend probe failed: embedded ACP runtime probe failed (agent=codex; command=npx @zed-industries/codex-acp@^0.11.1; cwd=/workspace; ACP connection closed)` at `2026-04-21T13:41:06.276+00:00`.
- Root cause confirmed from the log: the embedded runtime is attempting `npx @zed-industries/codex-acp@^0.11.1` at container runtime but the runtime `npx` fetch cannot complete because the container is `read_only: true` with `noexec`/`nosuid`/`nodev` tmpfs and cannot write the npm cache. Fix Option 2A (P4-T1) pre-bakes this package into the image at build time so no runtime `npx` is required.
- Gateway plugin count baseline = 5. After P4-T1 + P5-T3/P5-T4, the orchestrator-delegated post-recreate capture should show the plugin list include the ACP-backed entry (or plugin count grow by 1) and the `embedded acpx runtime backend probe failed` line must be absent.
