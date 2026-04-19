# Baseline Manifest Shape (Refinement 2026-04-19)

Timestamp: 2026-04-19T00-00
Command: `Read scripts/Publish.Helpers.psm1` lines 394-442 (`Write-PublishManifest` body) at HEAD `453343e`.
EXIT_CODE: 0
Output Summary:
- `Write-PublishManifest` (HEAD `453343e`) emits `[pscustomobject]@{ version; generatedAt; files }` where `version` is the `-Version` parameter, `generatedAt` is UTC ISO-8601, and `files` is an array of `{ path, size, sha256 }` entries sorted by `path` using invariant culture.
- Top-level keys observed: `version`, `generatedAt`, `files`.
- Sample `files[0]` keys: `path`, `size`, `sha256`.
- Refinement change required: drop `generatedAt`; keep only `{ version, files }` per PC-T10. Install scripts must appear as entries in `files` after the new staging stage runs (PC-T11).
