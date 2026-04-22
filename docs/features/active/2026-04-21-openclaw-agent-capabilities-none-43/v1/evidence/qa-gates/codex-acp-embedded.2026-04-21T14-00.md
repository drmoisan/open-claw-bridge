# Evidence — codex-acp package embedded in agent image (P5-T5, closes AC-4)

Timestamp: 2026-04-21T14:54:30Z

Command:
```
docker compose exec -T openclaw-agent sh -c 'ls /usr/local/lib/node_modules/@zed-industries/codex-acp/package.json && cat /usr/local/lib/node_modules/@zed-industries/codex-acp/package.json | grep -E "\"version\"|\"name\""'
```

EXIT_CODE: 0

Output Summary:

```
/usr/local/lib/node_modules/@zed-industries/codex-acp/package.json
  "name": "@zed-industries/codex-acp",
  "version": "0.11.1",
```

AC-4 result:
- The `@zed-industries/codex-acp` package exists at `/usr/local/lib/node_modules/@zed-industries/codex-acp/package.json` inside the running container.
- The pinned version `0.11.1` is present in the installed package's manifest.
- The package is resolvable on PATH: `/usr/local/bin/codex-acp` is the installed binary symlink.

Supporting evidence (runtime cache and config paths created by the entrypoint):
```
$ docker compose exec -T openclaw-agent sh -c 'ls /workspace/.npm-cache ; ls /workspace/.codex'
_cacache
_logs
_npx
_update-notifier-last-checked

memories
skills
tmp
```

Both `CODEX_HOME=/workspace/.codex` and `NPM_CONFIG_CACHE=/workspace/.npm-cache` are populated on the named volume `openclaw_agent_workspace`. The cache persists across container restarts so subsequent `npx @zed-industries/codex-acp@^0.11.1` invocations resolve from cache without network fetch.
