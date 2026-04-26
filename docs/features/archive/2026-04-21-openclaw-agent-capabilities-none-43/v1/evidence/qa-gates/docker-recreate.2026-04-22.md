# Docker Recreate Evidence — P2-T2

Timestamp: 2026-04-22T10:56:30Z
Command: docker compose up -d --force-recreate openclaw-agent
EXIT_CODE: 0

## Output Summary

Container recreated successfully from rebuilt image.

```
[+] up 1/1
 ✓ Container openclaw-agent Started    0.9s
```

Container `openclaw-agent` was stopped, removed, and restarted from the newly built image (`openclaw/agent:pre-mvp`) that embeds the updated seed configuration with `"profile": "coding"`. No errors or warnings. Command exited with code 0.
