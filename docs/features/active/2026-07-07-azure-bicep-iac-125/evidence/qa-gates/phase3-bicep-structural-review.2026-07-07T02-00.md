# Phase 3 Bicep/Markdown Structural Review — CLI-Unavailable Fallback

- Timestamp: 2026-07-07T02-00
- Command (manual structural review plus a secret-shaped-pattern grep: `AccountKey=`, `SharedAccessKey=`, `password =`, `secret =`, `connectionstring`, case-insensitive): `grep -niE "AccountKey=|SharedAccessKey=|password\s*=|secret\s*=|connectionstring" deploy/azure/parameters/main.dev.bicepparam`
- EXIT_CODE: 0
- Output Summary: no secret-shaped literal found; both files reviewed; overall result **PASS**.

| File | Check | Result |
|---|---|---|
| `deploy/azure/parameters/main.dev.bicepparam` | Contains `using 'main.bicep'` | Present |
| `deploy/azure/parameters/main.dev.bicepparam` | Binds `environmentName = 'dev'` | Present |
| `deploy/azure/parameters/main.dev.bicepparam` | `containerImage` bound to a clearly-labeled non-secret placeholder | Yes — value `'REPLACE_AT_DEPLOY_TIME/openclaw-core:unset'`, preceded by a comment stating it is a placeholder to override at deploy time; no registry credential present |
| `deploy/azure/parameters/main.dev.bicepparam` | Secret-shaped-pattern grep | No match |
| `deploy/azure/README.md` | Documents module layout (`main.bicep` + three modules + `parameters/main.dev.bicepparam`) | Present |
| `deploy/azure/README.md` | Contains `bicep build deploy/azure/main.bicep` verbatim | Present (1 occurrence) |
| `deploy/azure/README.md` | Contains the full `az deployment group create ...` command verbatim | Present (1 occurrence) |
| `deploy/azure/README.md` | States live deployment is out of scope, deferred to a separately-authored runbook | Present (2 "out of scope" occurrences, including the deployment-runbook pointer) |

## CLI-Unavailable Fallback Rationale

Neither `bicep` nor `az` CLI is installed locally (P0-T6). No CLI validation applies to `README.md` in any case (Markdown, not compiled). Real `bicep build` execution against `main.dev.bicepparam`'s binding compatibility with `main.bicep` occurs in CI via `.github/workflows/_bicep-validate.yml` (Phase 4).

## Overall Result: PASS
