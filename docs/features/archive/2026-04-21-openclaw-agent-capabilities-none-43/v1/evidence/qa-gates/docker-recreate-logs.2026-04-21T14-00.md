# Evidence — Agent container recreate and log verification (P5-T4)

Timestamp: 2026-04-21T14:54:23Z

Command: `docker compose up -d --force-recreate openclaw-agent && docker compose logs --tail=300 openclaw-agent`

EXIT_CODE: 0

Output Summary:

Baseline (pre-fix) ACP-backend log lines (captured 2026-04-21T13:40 from the pre-amendment image):

```
[gateway] ready (5 plugins: acpx, browser, device-pair, phone-control, talk-voice; 4.8s)
[plugins] embedded acpx runtime backend probe failed: embedded ACP runtime probe failed (agent=codex; command=npx @zed-industries/codex-acp@^0.11.1; cwd=/workspace; ACP connection closed)
```

Post-fix (with Phase 4 + Phase 4B + Phase 4C applied) ACP-backend log lines:

```
2026-04-21T14:54:16.637+00:00 [gateway] ready (5 plugins: acpx, browser, device-pair, phone-control, talk-voice; 4.7s)
2026-04-21T14:54:19.370+00:00 [plugins] embedded acpx runtime backend registered (cwd: /workspace)
2026-04-21T14:54:23.569+00:00 [plugins] embedded acpx runtime backend ready
```

Absence check:

```
$ docker compose logs openclaw-agent 2>&1 | grep -c "embedded acpx runtime backend probe failed"
0
```

AC-1 result:
- The `embedded acpx runtime backend probe failed` line is absent from post-fix logs.
- The `[plugins] embedded acpx runtime backend ready` line is present — the ACP runtime successfully completed the handshake and is attached to the gateway.

Note on the `5 plugins` count:
- The `acpx, browser, device-pair, phone-control, talk-voice` plugin names represent the five gateway-side plugin hosts. `acpx` is the ACP host plugin; it always registers. What changed between pre-fix and post-fix is whether the ACP *backend* probe behind `acpx` succeeded. The `ready` line at `14:54:23` is the post-fix observable that was previously `probe failed`.
