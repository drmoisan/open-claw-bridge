---
Timestamp: 2026-04-25T00-00
Policy Order:
  1. CLAUDE.md — checked; file does not exist in this repository. No standing instructions file found.
  2. .claude/rules/general-code-change.md — read and confirmed.
  3. .claude/rules/general-unit-test.md — read and confirmed.
  4. .claude/rules/powershell.md — read and confirmed.
---

## Policy Read Confirmation

### 1. CLAUDE.md
File does not exist at the repository root. No overriding standing instructions recorded.

### 2. .claude/rules/general-code-change.md
Read and confirmed. Key constraints applied to this change:
- Simplicity first; no unnecessary abstraction.
- File size limit: no production or test file may exceed 500 lines.
- Separation of concerns: I/O isolated; domain logic testable without external dependencies.
- Error handling: fail fast and explicitly.
- Mandatory toolchain loop: format → lint → test; restart from step 1 on any failure.

### 3. .claude/rules/general-unit-test.md
Read and confirmed. Key constraints applied to this change:
- Repository-wide line coverage must remain >= 80%.
- Any new module, class, or method must target >= 90% coverage.
- Tests must be independent, isolated, fast, deterministic, and readable.
- External dependencies must be mocked.
- No temporary files in tests.

### 4. .claude/rules/powershell.md
Read and confirmed. Key constraints applied to this change:
- Toolchain: format (PoshQC) → analyze (PSScriptAnalyzer) → test (Pester v5).
- All functions use CmdletBinding() with explicit parameter contracts.
- State-changing functions use SupportsShouldProcess.
- Wrapper function seam (preferred) for external process calls: Invoke-HostAdapterProcess.
- Mock wrapper functions in tests, not raw executables.
- Coverage >= 80% overall; >= 90% for new code.
