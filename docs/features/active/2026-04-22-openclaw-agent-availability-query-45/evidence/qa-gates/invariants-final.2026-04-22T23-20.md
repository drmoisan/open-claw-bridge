# Phase 6 — Final Invariants Check

Timestamp: 2026-04-22T23-20

## Invariant 1 — openclaw.json profile unchanged

Command: `grep '"profile"' deploy/docker/openclaw-assistant/openclaw.json`
EXIT_CODE: 0
Output Summary: `"profile": "coding"` present.

Byte-level confirmation via blob hash:

- Baseline (from `development`): `99125e795e2fdf8f6e6270183e66ecae512aa4d2`
- Post-change (HEAD):            `99125e795e2fdf8f6e6270183e66ecae512aa4d2`

Hashes match — `deploy/docker/openclaw-assistant/openclaw.json` is byte-identical to baseline.

## Invariants 2–5 — docker-compose.yml hardening

| Check | Command | Matches (post-change) | Status |
|---|---|---|---|
| `read_only: true` | `grep -n "read_only: true" docker-compose.yml` | lines 16, 44, 64, 83 | preserved |
| `cap_drop:` | `grep -n "cap_drop:" docker-compose.yml` | lines 17, 65 | preserved |
| `no-new-privileges:true` | `grep -n "no-new-privileges:true" docker-compose.yml` | lines 20, 68 | preserved |
| `noexec,nosuid,nodev` | `grep -n "noexec,nosuid,nodev" docker-compose.yml` | lines 22, 70, 71 | preserved |

All four pre-existing match counts are preserved. Line-number differences vs baseline are limited to the single-line ripple caused by the TZ insertion at line 76 (baseline line 82 → post-change line 83 for the bind-mount `read_only: true`). Every listed token is byte-identical to baseline.

Outcome: all invariants satisfied.
