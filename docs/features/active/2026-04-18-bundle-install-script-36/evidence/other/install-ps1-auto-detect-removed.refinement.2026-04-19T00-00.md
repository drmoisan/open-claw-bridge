# Install.ps1 Auto-Detect Removed (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: `rg -l 'Find-NewestPublishVersion' scripts/` (via Grep tool, files_with_matches mode).
EXIT_CODE: 0
Output Summary:
- Zero matches for `Find-NewestPublishVersion` across `scripts/`.
- `scripts/Install.ps1` no longer references the retired helper; bundle-selection stage now sets `$BundleRoot = $SourcePath` (which defaults to `$PSScriptRoot`) and reads the version via `Get-ManifestVersion`.
- `scripts/Install.Helpers.psm1` no longer defines or exports `Find-NewestPublishVersion` (PD-T2, PD-T5).
- Acceptance: PE-T6 passes.
