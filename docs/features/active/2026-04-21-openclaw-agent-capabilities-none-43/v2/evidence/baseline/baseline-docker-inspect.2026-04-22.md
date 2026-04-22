# Baseline — docker inspect openclaw-agent (Pre-Fix)

- Timestamp: 2026-04-22T10:53:00Z
- Command: `docker inspect openclaw-agent`
- EXIT_CODE: 0

## Output Summary

Container state: **running** (healthy)

Container ID: `7e70904b05f0c4a5d33b19748de005cf53406f97179f5e7eb1430b7646225a94`
Name: `/openclaw-agent`
Image: `sha256:510ce16bc0d5b78ca83ff2c7174bc25f4ea5ad646abcc6999f4c0be42c0d04e6`
Started at: `2026-04-22T14:19:02.78715547Z`
Health status: `healthy` (last 5 health checks all `ExitCode: 0`, output `{"ok":true,"status":"live"}`)

### Hardening Fields (Pre-Fix State)

| Field | Value | Present |
|---|---|---|
| `ReadonlyRootfs` | `true` | YES |
| `CapDrop` | `["ALL"]` | YES |
| `SecurityOpt` | `["no-new-privileges:true"]` | YES |
| `Tmpfs["/.openclaw"]` | `"size=64m,noexec,nosuid,nodev,uid=1654,gid=1654,mode=0755"` | YES (noexec, nosuid, nodev) |
| `Tmpfs["/tmp"]` | `"size=64m,noexec,nosuid,nodev"` | YES (noexec, nosuid, nodev) |

All six hardening tokens (`ReadonlyRootfs`, `CapDrop: ALL`, `no-new-privileges:true`, `noexec` on `/.openclaw`, `nosuid` on `/.openclaw`, `nodev` on `/.openclaw`) are confirmed present in the pre-fix container state.

### Seed Configuration Note

At the time of baseline capture, the running container was built from the image containing `"profile": "minimal"` (confirmed by P0-T7 grep). The Phase 1 fix will rebuild the image with `"profile": "coding"` and recreate the container.

### Port and Volume Bindings

- Port: `127.0.0.1:18789 -> 18789/tcp`
- Volume: `C:\ProgramData\OpenClaw\HostAdapter\adapter.token:/run/openclaw/hostadapter.token:ro`
- Volume: `openclaw_openclaw_agent_workspace:/workspace:rw`
