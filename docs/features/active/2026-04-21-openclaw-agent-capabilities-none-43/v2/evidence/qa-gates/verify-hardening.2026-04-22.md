# Container Hardening Verification Evidence — P3-T2

Timestamp: 2026-04-22T10:59:00Z
Command: docker inspect openclaw-agent
EXIT_CODE: 0

## Output Summary

All three required hardening tokens confirmed present in the recreated container:

| Field | Confirmed Value |
|---|---|
| `ReadonlyRootfs` | `true` |
| `CapDrop` | `["ALL"]` |
| `SecurityOpt` | `["no-new-privileges:true"]` |

Relevant excerpt from `docker inspect openclaw-agent`:
```json
"CapDrop": [
    "ALL"
],
"ReadonlyRootfs": true,
"SecurityOpt": [
    "no-new-privileges:true"
]
```

Container hardening is fully preserved after image rebuild and container recreation. Result: PASS
