# Phase 0 — Instructions Read Evidence (Issue #62)

Timestamp: 2026-06-05T22-09

Policy Order: per `.claude/skills/policy-compliance-order`:
1. CLAUDE.md (standing instructions, always loaded)
2. .claude/rules/general-code-change.md
3. .claude/rules/general-unit-test.md
4. Language/domain-specific rules in scope (PowerShell, C#)
5. Supporting policy: tonality

## Files Read

- [P0-T1] `.claude/rules/general-code-change.md` — read. Cross-language code-change policy. Key constraints captured: 500-line file-size cap on production/test/script files; mandatory seven-stage toolchain loop (format -> lint -> type-check -> arch -> unit -> contract -> integration), restart from step 1 on any failure or auto-fix; fail-fast error handling; isolate I/O from domain logic.
- [P0-T2] `.claude/rules/general-unit-test.md` — read. Cross-language unit-test policy. Key constraints captured: line coverage >= 85%, branch coverage >= 75% across all tiers; AAA test structure; no temp files in tests; tests must be deterministic (controllable clock, seeded RNG, no real sleeps); test files mirror source tree under `tests/`.
- [P0-T3] `.claude/rules/powershell.md` — read. PowerShell-specific policy. Key constraints captured: PowerShell 7+ compatibility; advanced functions with CmdletBinding; SupportsShouldProcess for state-changing actions; wrapper-function seam pattern (parameter name must not be `Args`); MCP toolchain `mcp__drm-copilot__run_poshqc_format` / `_analyze` / `_test`; per-batch cap of 3 production + 3 test files; mock the wrapper not the executable; mock signature parity with production parameter names.
- [P0-T4] `.claude/rules/csharp.md` — read. C#-specific policy. Key constraints captured: CSharpier formatting (`dotnet csharpier`); .NET analyzers via `dotnet build` with `TreatWarningsAsErrors=true`; nullable reference types enabled; file-scoped namespaces required; coverage >= 85% line / >= 75% branch; banned APIs (DateTime.Now/UtcNow, Thread.Sleep, Task.Delay) via BannedApiAnalyzers. NOTE: existing `MsixPackageTests.cs` uses MSTest + FluentAssertions (the established pattern for this test project); new tests follow the existing file's framework per the plan.
- [P0-T5] `.claude/rules/tonality.md` — read. Tone policy. Professional, factual, evidence-first wording; no humor, hyperbole, or decorative metaphor; restrained phrasing when uncertain.

## MCP Tool Name Reconciliation

The plan references MCP tools under the prefix `mcp__drmCopilotExtension__*`. In this execution environment the available tools are `mcp__drm-copilot__run_poshqc_format`, `mcp__drm-copilot__run_poshqc_analyze`, `mcp__drm-copilot__run_poshqc_analyze_autofix`, and `mcp__drm-copilot__run_poshqc_test` (matching `.claude/rules/powershell.md`). The executor uses the `mcp__drm-copilot__*` names. This is a tool-name alias only; the commands and their semantics are identical to those named in the plan.
