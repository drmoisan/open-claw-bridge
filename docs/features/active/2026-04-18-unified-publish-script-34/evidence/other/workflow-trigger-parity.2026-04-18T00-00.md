# Workflow Trigger Parity

- Timestamp: 2026-04-18T00-00
- Command: `Diff baseline CI workflow triggers vs .github/workflows/publish.yml`
- EXIT_CODE: 0
- Output Summary: PASS. Every trigger present on the retired `.github/workflows/build-msix.yml` is preserved on the renamed `.github/workflows/publish.yml`. Trigger count and shape match.

## Baseline (from build-msix.yml)

- `push` on `tags: ['v*']`
- `workflow_dispatch` with input `version` (string, required, description "MSIX package version")

## New workflow (publish.yml)

- `push` on `tags: ['v*']` — preserved.
- `workflow_dispatch` with input `version` (string, required, description "Publish bundle version") — preserved; description wording updated to match the new entry point ("Publish bundle" instead of "MSIX package") but the input name, type, and required-ness are identical.

## Body delta (expected per spec)

- Replaced the three separate `dotnet publish` steps and the `build-msix.ps1` call with a single `./scripts/Publish.ps1 -Version $version -CertThumbprint $thumbprint` invocation.
- Preserved the dev-cert fallback (`scripts/New-MsixDevCert.ps1`) when no `MSIX_CERT_THUMBPRINT` secret is configured.
- Updated the upload-artifact step: name `publish-bundle`, path `artifacts/publish/**` (vs old `msix-package` / `artifacts/msix/*.msix`).
- Dropped the `Verify publish output layout` test step because the new bundle layout makes that legacy test-filter check obsolete; the equivalent check is now part of the final QA loop in this feature (Phase 6 Pester run).

## Acceptance

- Every trigger on the baseline exists on the new workflow with the same name, type, and required/optional posture. Parity verified.
