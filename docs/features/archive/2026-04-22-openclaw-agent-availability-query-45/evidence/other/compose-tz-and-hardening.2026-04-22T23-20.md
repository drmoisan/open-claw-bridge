# AC-6 Verification — docker-compose.yml TZ and hardening

Timestamp: 2026-04-22T23-20
Command: git diff development -- docker-compose.yml
EXIT_CODE: 0

## Output Summary

### Diff (verbatim)

```
diff --git a/docker-compose.yml b/docker-compose.yml
index d33d54d..b43dee9 100644
--- a/docker-compose.yml
+++ b/docker-compose.yml
@@ -73,6 +73,7 @@ services:
       OpenClaw__HostAdapter__BaseUrl: ${OpenClaw__HostAdapter__BaseUrl:-http://host.docker.internal:4319/v1}
       OpenClaw__HostAdapter__TokenFile: /run/openclaw/hostadapter.token
       OPENCLAW_GATEWAY_TOKEN: ${OPENCLAW_GATEWAY_TOKEN}
+      TZ: "America/New_York"
     ports:
       - "127.0.0.1:${OPENCLAW_AGENT_PORT:-18789}:18789"
     volumes:
```

- Added lines: 1 (`TZ: "America/New_York"`)
- Removed lines: 0
- Reformatting of other lines: none

### Hardening invariants — byte-identical

Pre-change (from baseline grep, `git show development:docker-compose.yml`) and post-change greps produce identical matches for the `openclaw-agent` service:

| Check | Pre-change line | Post-change line | Status |
|---|---|---|---|
| `read_only: true` | 64 | 64 | preserved |
| `cap_drop:` | 65 | 65 | preserved |
| `  - ALL` (cap_drop member) | 66 | 66 | preserved |
| `no-new-privileges:true` | 68 | 68 | preserved |
| `/tmp:size=64m,noexec,nosuid,nodev` | 70 | 70 | preserved |
| `/.openclaw:size=64m,noexec,nosuid,nodev,...` | 71 | 71 | preserved |

Bind-mount `read_only: true` for the token volume shifted from line 82 to line 83 as a natural line-number ripple from the +1 insertion above it; the line content itself is byte-identical.

### TZ placement

`grep -n "TZ:" docker-compose.yml` → exactly one match at line 76, inside the `openclaw-agent` service `environment:` block, with value `"America/New_York"`.

AC-6: SATISFIED
