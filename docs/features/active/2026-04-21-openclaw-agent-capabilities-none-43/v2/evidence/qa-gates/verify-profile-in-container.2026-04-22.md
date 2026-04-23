# Runtime Profile Verification Evidence — P3-T3

Timestamp: 2026-04-22T11:00:00Z
Command: docker compose exec openclaw-agent grep '"profile"' /.openclaw/openclaw.json
EXIT_CODE: 0

## Output Summary

```
    "profile": "coding"
```

`"profile": "coding"` confirmed inside the running container at `/.openclaw/openclaw.json`. The entrypoint successfully copied the updated seed file (from `/opt/openclaw-assistant-seed/openclaw.json`) to the runtime config location. The agent will now load the `coding` tool profile, which includes `group:runtime` (exec, process, code_execution) tools required to call the HostAdapter via curl/bash.

Result: PASS
