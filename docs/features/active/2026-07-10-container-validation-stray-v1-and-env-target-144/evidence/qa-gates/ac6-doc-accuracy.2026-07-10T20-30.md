# AC6 — Dashboard Documentation Accuracy (Issue #144, P2-T7)

- Timestamp: 2026-07-10T20-30
- Command: `grep` over `README.md` and `docs/mailbridge-runbook.md` for the removed inaccurate claims and the required corrected content (see per-pattern commands below).
- EXIT_CODE: 0

## Removed inaccurate claims (expected: zero matches)

| Pattern | README.md | docs/mailbridge-runbook.md |
|---|---|---|
| `reads the token without an operator paste step` | 0 | 0 |
| `will not accept any other credential` | 0 | 0 |
| `only credential the dashboard accepts` | 0 | 0 |

Commands: `grep -rc "<pattern>" README.md docs/mailbridge-runbook.md`. All three removed-claim patterns return zero matches across both files.

## Corrected content present (expected: >= 1)

| Content | README.md | docs/mailbridge-runbook.md |
|---|---|---|
| `#token=` URL fragment procedure | 3 | 4 |
| `openclaw devices clear` re-pair step | 1 | 1 |
| `OPENCLAW_AGENT_IMAGE` floating-tag note ("floating upstream tag") | 1 | 1 |

## Output Summary

AC6 satisfied. Both files no longer claim tokenless/auto dashboard access or that the token is the only accepted credential. Both now document the accurate upstream (OpenClaw 2026.6.11) procedure: the `http://127.0.0.1:.../#token=<OPENCLAW_GATEWAY_TOKEN>` fragment URL or Control UI paste, the device re-pair reset (`openclaw devices clear` inside the agent container + clear browser site data + reopen the fragment URL), and the floating-`OPENCLAW_AGENT_IMAGE`-tag caveat that the auth flow can change across upgrades.
