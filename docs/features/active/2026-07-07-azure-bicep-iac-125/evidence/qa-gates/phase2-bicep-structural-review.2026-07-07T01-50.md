# Phase 2 Bicep Structural Review — CLI-Unavailable Fallback

- Timestamp: 2026-07-07T01-50
- Command (manual structural review, no `bicep` CLI available; see `evidence/baseline/cli-tooling-availability.2026-07-07T01-32.md`): visual inspection of module reference paths, parameter/output wiring between `main.bicep` and its three modules, brace balance, and `containerApp.bicep`'s identity/output declarations.
- EXIT_CODE: 0
- Output Summary: both files reviewed; overall result **PASS**.

| File | Braces balanced | Key checks | Result |
|---|---|---|---|
| `deploy/azure/modules/containerApp.bicep` | 10 `{` / 10 `}` | Declares `identity: { type: 'SystemAssigned' }`; declares `containerImage` param with no default value; declares outputs `containerAppFqdn` and `containerAppPrincipalId`; ingress `targetPort: 8081` matches `deploy/docker/openclaw-core.Dockerfile`'s `EXPOSE 8081` | PASS |
| `deploy/azure/main.bicep` | 23 `{` / 23 `}` (includes balanced `${...}` string-interpolation braces) | Declares all four parameters (`environmentName` default `'dev'`, `location` default `resourceGroup().location`, `resourceNamePrefix` default `'openclaw'`, `containerImage` required, no default); references `modules/containerApp.bicep`, `modules/keyVault.bicep`, `modules/queue.bicep` via relative `module` paths that resolve to files created in Phase 1/this phase; declares all five outputs (`containerAppFqdn`, `containerAppPrincipalId`, `keyVaultUri`, `serviceBusNamespaceEndpoint`, `serviceBusQueueName`), each sourced from the matching module's `.outputs.*` | PASS |

## Module Call-Site Parameter Cross-Check

| Module | Declared params | Params supplied at call site in `main.bicep` | Match |
|---|---|---|---|
| `containerApp.bicep` | `containerAppEnvName`, `containerAppName`, `location`, `containerImage`, `tags` (default `{}`) | `containerAppEnvName`, `containerAppName`, `location`, `containerImage`, `tags` | Yes |
| `keyVault.bicep` | `keyVaultName`, `location`, `tags` (default `{}`) | `keyVaultName`, `location`, `tags` | Yes |
| `queue.bicep` | `serviceBusNamespaceName`, `queueName`, `location`, `tags` (default `{}`) | `serviceBusNamespaceName`, `queueName`, `location`, `tags` | Yes |

## CLI-Unavailable Fallback Rationale

Same rationale as Phase 1: neither `bicep` nor `az` CLI is installed locally (P0-T6). Real `bicep build` execution against these files occurs in CI via `.github/workflows/_bicep-validate.yml` (Phase 4), on the `windows-latest` runner, not claimed as a local result here.

## Overall Result: PASS
