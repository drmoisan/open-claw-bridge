# Phase 0 Policy-Read Evidence — Issue #135

Timestamp: 2026-07-07T15-30

Policy Order:
1. `CLAUDE.md`
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/powershell.md`

## Files Read

- [P0-T1] `CLAUDE.md` — attempted read at repository root. This file does not exist in this repository (verified via directory listing and file-existence check at the repo root). The repository's standing-instructions file is `AGENTS.md`, not `CLAUDE.md`. This absence is noted for the record; the remaining three policy files were read in full and govern this execution.
- [P0-T2] `.claude/rules/general-code-change.md` — read in full. Cross-language code change policy: design principles (simplicity, reusability, extensibility, separation of concerns), class/function guidance, module rigor tiers reference, mandatory seven-stage toolchain loop, 500-line file size limit, error handling/logging, naming conventions, public API compatibility, dependency policy, I/O boundary isolation.
- [P0-T3] `.claude/rules/general-unit-test.md` — read in full. Cross-language unit test policy: five core test properties (independence, isolation, fast execution, determinism, readability), coverage requirements (line >= 85%, branch >= 75%, uniform across tiers), coverage exclusion policy (no production file may be excluded), scenario completeness, AAA structure, external-dependency isolation, test file location mirroring `tests/`, documentation, test categories, determinism infrastructure.
- [P0-T4] `.claude/rules/powershell.md` — read in full. PowerShell-specific toolchain (format -> analyze -> test via PoshQC MCP commands), PowerShell 7+ compatibility, coding standards (advanced functions, ShouldProcess, no global state, no Invoke-Expression/secrets), change budget (direct-mode up to 2 production files, per-batch cap 3+3), design seams (wrapper function seam preferred), testing standards (Pester v5, coverage thresholds), deterministic test requirements, mocking rules, prohibited behaviors.

## Notes

- `CLAUDE.md` is absent at the repository root; `AGENTS.md` is the file actually present at that location and appears to serve the equivalent standing-instructions role in this repository. This is recorded as an observation only — execution proceeds per the plan's explicit four-file Required References list (`.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/powershell.md`), all three of which were read successfully.
