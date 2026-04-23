---
Timestamp: 2026-04-23T12-40
Command: (Get-Content .github/workflows/ci.yml | Measure-Object -Line).Lines
EXIT_CODE: 0
---

# ci.yml Rewrite Summary (P2-T1)

Output Summary:
- Final line count: **89** (limit is strictly < 300; AC-2 constraint satisfied with large margin).
- Final jobs:
  - `dotnet-build-test` (windows-latest): checkout -> setup-dotnet@v4 (10.0.x) -> restore -> build `/warnaserror` -> test with `XPlat Code Coverage` -> upload `TestResults/**`.
  - `powershell-quality` (windows-latest): checkout -> install Pester 5.7.1 + PSScriptAnalyzer -> `Invoke-ScriptAnalyzer -Path scripts, tests/scripts -Recurse -Severity Warning,Error` with fail-on-nonempty -> `Invoke-Pester -Path tests/scripts -Output Detailed -CI` -> upload `artifacts/pester/**`.
  - `actionlint` (ubuntu-latest): checkout -> install via official release downloader (pinned tag `v1.7.7`) -> `./actionlint -color`.
- All jobs listed in the pre-adaptation template (`quality-checks7`, `security-scan`, `docs-validation`, `build-check`, `poshqc`, `shell-coverage`, `drm-copilot-extension-tests`) were removed.
- PoshQC wrapper references removed in accordance with the amended AC-4 in `issue.md` (the wrapper module does not exist in this repository).
- Job renamed from `powershell-qc` to `powershell-quality` to avoid a substring collision with the excluded token `shell-qc`.
