# Gateway Log Verification Evidence — P3-T1

Timestamp: 2026-04-22T10:58:00Z
Command: docker compose logs openclaw-agent
EXIT_CODE: 0

Secondary Command: docker compose logs openclaw-agent | Select-String "embedded acpx runtime backend probe failed" | Measure-Object -Line
Secondary EXIT_CODE: 0

## Output Summary

Gateway ready line confirmed:
```
2026-04-22T15:37:42.329+00:00 [gateway] ready (5 plugins: acpx, browser, device-pair, phone-control, talk-voice; 3.1s)
```

acpx backend ready line confirmed:
```
2026-04-22T15:37:48.647+00:00 [plugins] embedded acpx runtime backend ready
```

Probe failure count: 0 (Lines: 0)
`Select-String "embedded acpx runtime backend probe failed" | Measure-Object -Line` returned 0 lines.

Config overwrite was observed at startup (expected — entrypoint copies seed to runtime location):
```
2026-04-22T15:37:41.832+00:00 Config overwrite: /.openclaw/openclaw.json (sha256 ... -> f3c93211..., changedPaths=1)
```
This confirms the seed file (with "profile": "coding") was applied to the runtime config.

Result: PASS
