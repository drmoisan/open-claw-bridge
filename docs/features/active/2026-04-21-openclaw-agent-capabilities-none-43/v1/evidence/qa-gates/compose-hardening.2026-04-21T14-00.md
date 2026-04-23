---
Timestamp: 2026-04-21T15:10:30Z
Purpose: Phase 6 P6-T7 — compose hardening preservation check for AC-5
---

# Final — Compose Hardening Preservation (P6-T7)

Timestamp: 2026-04-21T15:10:30Z

Command A: `git diff origin/development...HEAD -- docker-compose.yml`
Command B: `git diff HEAD -- docker-compose.yml` (working-tree delta)
Command C: `grep -nE 'cap_drop:|read_only: true|no-new-privileges|noexec|nosuid|nodev' docker-compose.yml`

EXIT_CODE: 0

Output Summary: `docker-compose.yml` unchanged vs. `origin/development` merge-base and unchanged vs. HEAD. All required hardening tokens remain present at their expected lines.

- `git diff origin/development...HEAD -- docker-compose.yml` — empty diff, exit 0.
- `git diff HEAD -- docker-compose.yml` — empty diff, exit 0 (no working-tree modification to `docker-compose.yml` either).
- Hardening tokens still present in `docker-compose.yml`:
  - Line 16: `    read_only: true` (mailbridge service)
  - Line 17: `    cap_drop:`
  - Line 20: `      - no-new-privileges:true`
  - Line 22: `      - /tmp:size=256m,noexec,nosuid,nodev`
  - Line 44: `        read_only: true`
  - Line 64: `    read_only: true` (openclaw-agent service)
  - Line 65: `    cap_drop:`
  - Line 68: `      - no-new-privileges:true`
  - Line 70: `      - /tmp:size=64m,noexec,nosuid,nodev`
  - Line 71: `      - /.openclaw:size=64m,noexec,nosuid,nodev,uid=1654,gid=1654,mode=0755`
  - Line 82: `        read_only: true`

All six required hardening tokens (`cap_drop:`, `read_only: true`, `no-new-privileges`, `noexec`, `nosuid`, `nodev`) are present.

Result: PASS. AC-5 is met — `docker-compose.yml` is byte-identical to `origin/development` and all hardening constraints remain active on both `mailbridge` and `openclaw-agent` services.
