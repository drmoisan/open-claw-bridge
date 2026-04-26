# Baseline — docker-compose.yml `start_period` Values

Timestamp: 2026-04-18T00-00
Command: `rg 'start_period' docker-compose.yml`
EXIT_CODE: 0
Output Summary: `openclaw-core` service uses `start_period: 20s` (line 50). `openclaw-agent` service uses `start_period: 30s` (line 85). These values are the authoritative reference for the Q1 health-poll ceiling. The planner decision of 90s timeout with 3s poll interval provides a 60-second safety margin above the higher `start_period` (30s) and yields >= 10 poll cycles after that period expires.

## Raw grep output

```
50:      start_period: 20s
85:      start_period: 30s
```

## Service mapping (verified by reading docker-compose.yml context)

| Service          | Line | start_period |
|------------------|------|--------------|
| `openclaw-core`  | 50   | 20s          |
| `openclaw-agent` | 85   | 30s          |

## Planner decision Q1 alignment

- Total bounded timeout: 90 seconds.
- Poll interval: 3 seconds.
- Safety margin above the longer `start_period` (30s): 60 seconds.
- Poll cycles after `start_period` expiry: >= 20 (90s - 30s = 60s / 3s = 20 cycles).

The defaults are exposed on `Wait-ComposeHealthy` via `-TimeoutSeconds 90` and `-PollIntervalSeconds 3`.
