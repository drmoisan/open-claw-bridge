# Dockerfile v1 Token Verification Evidence — P4-T4

Timestamp: 2026-04-22T11:04:00Z

## Commands and Results

Command 1: Select-String "codex-acp" deploy/docker/openclaw-agent.Dockerfile | Measure-Object
EXIT_CODE: 0
Count: 3

Command 2: Select-String "CODEX_HOME" deploy/docker/openclaw-agent.Dockerfile | Measure-Object
EXIT_CODE: 0
Count: 1

Command 3: Select-String "NPM_CONFIG_CACHE" deploy/docker/openclaw-agent.Dockerfile | Measure-Object
EXIT_CODE: 0
Count: 1

## Output Summary

All three Dockerfile v1 tokens are present and intact:

| Token | Count | Status |
|---|---|---|
| `codex-acp` (global npm install of `@zed-industries/codex-acp`) | 3 | PRESENT (≥1) |
| `CODEX_HOME` (`ENV CODEX_HOME=/workspace/.codex`) | 1 | PRESENT (≥1) |
| `NPM_CONFIG_CACHE` (`ENV NPM_CONFIG_CACHE=/workspace/.npm-cache`) | 1 | PRESENT (≥1) |

Dockerfile v1 changes from the AC-4/AC-4B/AC-4C fix phases are fully preserved. No regression introduced by the v2 openclaw.json seed file change.

Result: PASS
