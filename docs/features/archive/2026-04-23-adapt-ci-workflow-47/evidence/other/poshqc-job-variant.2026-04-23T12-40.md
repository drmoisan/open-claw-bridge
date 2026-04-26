---
Timestamp: 2026-04-23T12-40
Command: n/a (policy decision record)
EXIT_CODE: 0
---

# PowerShell Job Variant Decision

Plan DF-1 defined two variants:
- **Variant A** (module present): call `Install-PoshQCTools`, `Invoke-PoshQCFormat`, `Invoke-PoshQCAnalyze`, `Invoke-PoshQCTest` without `hashFiles()` guards.
- **Variant B** (module absent): the same four calls with `if: hashFiles('scripts/powershell/PoshQC/PoshQC.psm1') != ''` guards.

**Superseding decision (AC-4 amendment in `issue.md`):**

Neither variant was emitted. `issue.md` was amended to require the CI job to call `Invoke-ScriptAnalyzer` and `Invoke-Pester` directly, because the PoshQC wrapper does not exist in this repository (MCP-extension-based tooling is not invocable from a GitHub Actions runner). Variant B would have guaranteed a job that always no-ops on this branch, which fails the intent of AC-4. Variant A would reference a non-existent import path.

**Emitted job (source `.github/workflows/ci.yml` lines 40-78):**
- `powershell-quality` (windows-latest)
- Installs Pester 5.7.1 and PSScriptAnalyzer via `Install-Module -Scope CurrentUser -Force`.
- Runs `Invoke-ScriptAnalyzer -Path scripts, tests/scripts -Recurse -Severity Warning,Error`; fails if any Warning/Error results.
- Runs `Invoke-Pester -Path tests/scripts -Output Detailed -CI`.
- Uploads `artifacts/pester/**` with `if-no-files-found: ignore`.

Output Summary:
- P0-T7 established `MODULE_ABSENT`; AC-4 amendment was authoritative; the emitted job reflects the amendment.
- Decision preserves the issue invariant "no reference to the non-existent PoshQC wrapper" while keeping the PowerShell QC workflow job functional.
