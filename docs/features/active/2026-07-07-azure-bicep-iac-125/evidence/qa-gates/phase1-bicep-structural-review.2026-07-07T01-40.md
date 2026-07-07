# Phase 1 Bicep Structural Review — CLI-Unavailable Fallback

- Timestamp: 2026-07-07T01-40
- Command (manual structural review, no `bicep` CLI available; see `evidence/baseline/cli-tooling-availability.2026-07-07T01-32.md`): visual inspection of parameter/resource/output declarations, brace balance (`grep -o '{' | wc -l` vs. `grep -o '}' | wc -l`), and a secret-shaped-literal grep (`AccountKey`, `SharedAccessKey`, `password`, `secret =`, `connectionString`).
- EXIT_CODE: 0
- Output Summary: both files reviewed; overall result **PASS**.

| File | Braces balanced | Resource/param/output declarations present | `enableRbacAuthorization: true` present (keyVault only) | Secret-shaped literal found | Result |
|---|---|---|---|---|---|
| `deploy/azure/modules/keyVault.bicep` | 4 `{` / 4 `}` | Yes — `param keyVaultName`, `param location`, `param tags`, `resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01'`, `output keyVaultUri`, `output keyVaultResourceId`, `output keyVaultName` | Yes (line `enableRbacAuthorization: true`) | None | PASS |
| `deploy/azure/modules/queue.bicep` | 4 `{` / 4 `}` | Yes — `param serviceBusNamespaceName`, `param queueName`, `param location`, `param tags`, `resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview'`, `resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview'`, `output serviceBusNamespaceEndpoint`, `output serviceBusQueueName` | n/a | None (no connection-string output declared; only host-name-shaped `serviceBusEndpoint` and the queue name) | PASS |

## CLI-Unavailable Fallback Rationale

Neither the `bicep` CLI nor the `az` CLI is installed in this local execution environment (verified at P0-T6). This structural review substitutes for a local `bicep build` pass. Real `bicep build` execution against these files occurs in CI via `.github/workflows/_bicep-validate.yml` (authored in Phase 4 of this plan), on the `windows-latest` GitHub Actions runner, exercised when `ci.yml` is driven (e.g. a PR or `gh workflow run ci.yml`) — not claimed as a local result by this task.

## Overall Result: PASS
