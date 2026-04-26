# Baseline — Pre-change file hashes

Timestamp: 2026-04-22T23-20
Command: git hash-object deploy/docker/openclaw-assistant/USER.md deploy/docker/openclaw-assistant/AGENTS.md deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md deploy/docker/openclaw-assistant/TOOLS.md docker-compose.yml src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs src/OpenClaw.MailBridge/OutlookScanner.cs src/OpenClaw.MailBridge/CacheRepository.cs
EXIT_CODE: 0

## Output Summary

| File | Pre-change SHA-1 blob hash |
|---|---|
| `deploy/docker/openclaw-assistant/USER.md` | `1443a2069b7111da6018fc0b2e1b013d055730be` |
| `deploy/docker/openclaw-assistant/AGENTS.md` | `27463810771357ab24cbf7245fd03921ac79bc1b` |
| `deploy/docker/openclaw-assistant/skills/mailbridge_admin/SKILL.md` | `a3fb09a8bf9019070d5a87363fbb1fbaee311569` |
| `deploy/docker/openclaw-assistant/TOOLS.md` | `631ebce203a12bf5984ee501b059ed76f58ab2b6` |
| `docker-compose.yml` | `d33d54d39c3b64e030381af977ea3209ee60fdf0` |
| `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs` | `c032d4e359015487e0acdf599b4b7951c3a3b3a1` |
| `src/OpenClaw.MailBridge/OutlookScanner.cs` | `68c803cdb25361deaf8073cff0c46de5d8db001c` |
| `src/OpenClaw.MailBridge/CacheRepository.cs` | `0c6d74404e2211bd4da2f3c9cc9984905222ddae` |

These hashes anchor the eight files whose content this plan is authorized to modify. Files not listed here must not change. The invariant file `deploy/docker/openclaw-assistant/openclaw.json` is deliberately excluded from this table: it must remain byte-identical to the `development` tree.
