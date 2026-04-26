# Baseline CI Workflow (build-msix.yml)

- Timestamp: 2026-04-18T00-00
- Command: `Read-Item .github/workflows/build-msix.yml` (captured via Read tool)
- EXIT_CODE: 0
- Output Summary: Baseline snapshot of `.github/workflows/build-msix.yml` (55 lines). Triggers captured for Phase 5 rename parity check. Triggers: `push` on tags matching `v*`, and `workflow_dispatch` with a required `version` string input.

## Triggers

- `push` with `tags: ['v*']` — fires on any push of a tag beginning with `v`.
- `workflow_dispatch` with `inputs.version` (required, string, description "MSIX package version").

## Environment

- Job name: `build-msix`
- Runner: `windows-latest`

## Steps (current)

1. `Checkout` — `actions/checkout@v4`.
2. `Setup .NET SDK` — `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x`.
3. `Publish bridge` — `dotnet publish src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj /p:PublishProfile=msix`.
4. `Publish client` — `dotnet publish src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj /p:PublishProfile=msix`.
5. `Verify publish output layout` — runs the MailBridge tests filtered to `PublishOutput_` cases; env `MSIX_PUBLISH_DIR`.
6. `Build MSIX package` — pwsh: calls `scripts/New-MsixDevCert.ps1` then `scripts/build-msix.ps1` with the trimmed version and cert thumbprint.
7. `Upload MSIX artifact` — `actions/upload-artifact@v4`, name `msix-package`, path `artifacts/msix/*.msix`.

## Parity expectations for Phase 5 rename

- The renamed `.github/workflows/publish.yml` MUST preserve these triggers verbatim (push `v*` tags + `workflow_dispatch` with `version` input).
- The body replaces steps 3-6 with a single `pwsh ./scripts/Publish.ps1 ... -CertThumbprint <secret>` invocation (or `-SkipSign` when no secret is set).
- The upload-artifact step's `path` input should be updated to point at the new bundle location under `artifacts/publish/<version>/`.
