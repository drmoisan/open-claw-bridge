---
Timestamp: 2026-04-23T12-40
Commands:
  - Select-String for required amended AC-4 patterns
  - Select-String for forbidden PoshQC wrapper references
EXIT_CODE: 0
---

# verify-ci-powershell-job — AC-4 (amended) evidence

AC-4 amendment (per `docs/features/active/2026-04-23-adapt-ci-workflow-47/issue.md`):
The PowerShell workflow job must call `Invoke-ScriptAnalyzer` and `Invoke-Pester` directly, using `Install-Module -Scope CurrentUser` for Pester v5.x and PSScriptAnalyzer. It must **not** reference the non-existent PoshQC wrapper.

## Required patterns (amended AC-4)

| Pattern | Line | Matched text |
|---|---|---|
| `Install-Module -Name Pester` | 50 | `Install-Module -Name Pester -RequiredVersion 5.7.1 -Scope CurrentUser -Force -SkipPublisherCheck` |
| `Install-Module -Name PSScriptAnalyzer` | 51 | `Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force` |
| `Invoke-ScriptAnalyzer` (step name) | 53 | `- name: Analyze PowerShell (Invoke-ScriptAnalyzer)` |
| `Invoke-ScriptAnalyzer` (call) | 56 | `$results = Invoke-ScriptAnalyzer -Path scripts, tests/scripts -Recurse -Severity Warning,Error` |
| `scripts, tests/scripts` (AC-4 required scope) | 56 | same as above |
| `-Severity Warning,Error` (AC-4 required severity) | 56 | same as above |
| `Invoke-Pester` (step name) | 63 | `- name: Test PowerShell (Invoke-Pester)` |
| `Invoke-Pester` (call) | 66 | `Invoke-Pester -Path tests/scripts -Output Detailed -CI` |
| `-Path tests/scripts` | 66 | same as above |
| `-Output Detailed -CI` | 66 | same as above |
| `artifacts/pester` (upload path) | 73 | `path: artifacts/pester/**` |

Additional fail-on-nonempty gate:
- Lines 57-61 (`if ($results) { ... Write-Error ...; exit 1 }`) ensure the job fails when `Invoke-ScriptAnalyzer` emits any Warning or Error result, matching the AC-4 amendment requirement "fail if the result is non-empty".

Pester artifact upload:
- Line 70-75: `actions/upload-artifact@v4` with `path: artifacts/pester/**` and `if-no-files-found: ignore` (matches AC-4 amendment).

## Forbidden patterns (AC-4 amendment; must be absent)

| Pattern | Result |
|---|---|
| `PoshQC.psm1` | ABSENT |
| `Invoke-PoshQCFormat` | ABSENT |
| `Invoke-PoshQCAnalyze` | ABSENT |
| `Invoke-PoshQCTest` | ABSENT |
| `Install-PoshQCTools` | ABSENT |

Output Summary:
- The `powershell-quality` job on `windows-latest` implements the amended AC-4 verbatim.
- No reference to the non-existent PoshQC wrapper appears anywhere in `.github/workflows/ci.yml`.
