Timestamp: 2026-07-10T15-02

Policy Order:
1. `CLAUDE.md`
2. `.claude/rules/general-code-change.md`
3. `.claude/rules/general-unit-test.md`
4. `.claude/rules/powershell.md`

Files read (in order):
1. `CLAUDE.md` — attempted read; file does not exist at the repository root in this worktree (`C:\Users\DanMoisan\repos\open-claw-bridge\.claude\worktrees\agent-a8a9a59589481ca11\CLAUDE.md`). No standing-instructions file is present to read; recorded as checked/absent rather than skipped.
2. `.claude/rules/general-code-change.md` — read in full. Cross-language code change policy: simplicity/reusability/extensibility/separation-of-concerns design priorities, mandatory seven-stage toolchain loop (format -> lint -> type-check -> architecture -> unit -> contract -> integration, restart on any failure/file-change), 500-line file cap, fail-fast error handling, naming conventions, API compatibility, dependency policy, I/O boundary isolation.
3. `.claude/rules/general-unit-test.md` — read in full. Coverage requirements (line >= 85%, branch >= 75%, uniform across tiers), coverage exclusion policy (no production file may be excluded), scenario completeness, AAA test structure, external-dependency isolation (no temp files), test file location mirroring `tests/`, determinism infrastructure requirements.
4. `.claude/rules/powershell.md` — read in full. PoshQC toolchain (format -> analyze -> test via MCP commands `mcp__drm-copilot__run_poshqc_format` / `run_poshqc_analyze` / `run_poshqc_test`), PowerShell 7+ compatibility, advanced-function coding standards, direct-mode change budget (up to 2 production files), wrapper-function design-seam pattern (preferred), Pester v5 testing standards, mocking rules (mock the wrapper, not the executable; parameter parity), prohibited behaviors (no generic runner frameworks, no weakened assertions, no sleeps/retries).

This artifact covers plan tasks P0-T1 through P0-T5.
