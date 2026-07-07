# CLI Tooling Availability Check (P0-T6)

- Timestamp: 2026-07-07T01-32
- Command: `pwsh -NoProfile -Command "Get-Command bicep -ErrorAction SilentlyContinue; Get-Command az -ErrorAction SilentlyContinue"`
- EXIT_CODE: 0
- Output Summary: Both `Get-Command bicep -ErrorAction SilentlyContinue` and `Get-Command az -ErrorAction SilentlyContinue` returned no result (empty) in this local execution environment, confirming neither the Bicep CLI nor the Azure CLI is installed here. Every `bicep build` verification task in this plan therefore uses the documented structural-review fallback rather than a fabricated CLI-pass claim. Real `bicep build` execution occurs in `.github/workflows/_bicep-validate.yml` (Phase 4 of this plan), on the `windows-latest` GitHub Actions runner, where Azure CLI and Bicep are preinstalled per the research artifact (`docs/features/active/2026-07-07-azure-bicep-iac-125/research/2026-07-07-bicep-iac-architecture.md`, §6).
