# Phase 0 Baseline PoshQC Pester Run

Timestamp: 2026-04-26T22-50

Command: `mcp__drmCopilotExtension__run_poshqc_test` (workspace_root=c:\Users\DanMoisan\repos\open-claw-bridge)

EXIT_CODE: 0

Output Summary:
- Tool returned `{"ok":true}`.
- `artifacts/pester/pester-junit.xml` (regenerated 2026-04-26 21:08): 189 tests, 0 failures, 0 errors, 0 disabled, runtime 16.680s.
- Numeric line-coverage source for the Install layer (primary MCP artifact `artifacts/pester/powershell-coverage.xml` is scoped to `.claude/hooks` only and does not enumerate `Install.ps1` or `Install.Helpers.psm1`):
  - Repository-wide line coverage (Install + Publish + Uninstall packages, from `artifacts/pester/coverage-final.refinement.xml` produced 2026-04-19): **95.98%** (430 covered / 18 missed lines on 448 tracked lines).
  - Install-layer line coverage (Install.ps1 + Install.Helpers.psm1, same artifact): **95.26%** (201 covered / 10 missed).
- Coverage capture scope mirrors the prior `2026-04-25-install-hostadapter-not-started-59` baseline pattern: primary MCP coverage artifact is `artifacts/pester/powershell-coverage.xml`; supplementary Install-layer numeric source is `artifacts/pester/coverage-final.refinement.xml`. P7 will follow the same dual-source pattern when computing post-change coverage.

SearchScope:
- `c:\Users\DanMoisan\repos\open-claw-bridge\artifacts\pester\pester-junit.xml`
- `c:\Users\DanMoisan\repos\open-claw-bridge\artifacts\pester\powershell-coverage.xml`
- `c:\Users\DanMoisan\repos\open-claw-bridge\artifacts\pester\coverage-final.refinement.xml`

SearchPatterns:
- `<testsuites ... tests=...>` for total/failure counts in junit
- `<package name="scripts">` and `<counter type="LINE" .../>` for line coverage
- `<class name="scripts/Install" .../>` and `<class name="scripts/Install.Helpers" .../>` for Install-layer counters

SearchResult:
- junit summary located at line 2 of `pester-junit.xml`.
- `powershell-coverage.xml` contains only `.claude/hooks` package (counter LINE missed=284 covered=0).
- `coverage-final.refinement.xml` package "scripts" report-level counter LINE missed=18 covered=430.
