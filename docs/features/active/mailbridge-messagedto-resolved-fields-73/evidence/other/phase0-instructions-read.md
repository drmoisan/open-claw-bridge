# Phase 0 — Policy Instructions Read (Issue #73, Cycle 1)

Timestamp: 2026-06-14T09-10

Policy Order:
1. CLAUDE.md (standing instructions) — NOT PRESENT in this repository. No root `CLAUDE.md` exists; standing instructions are delivered via path-scoped `.claude/rules/` frontmatter.
2. .claude/rules/general-code-change.md (cross-language code change policy)
3. .claude/rules/general-unit-test.md (cross-language unit test policy)
4. Language/domain-specific rules in scope for C#:
   - .claude/rules/csharp.md
   - .claude/rules/architecture-boundaries.md
   - .claude/rules/quality-tiers.md

Files read (explicit list):
- .claude/rules/general-code-change.md
- .claude/rules/general-unit-test.md
- .claude/rules/csharp.md
- .claude/rules/architecture-boundaries.md
- .claude/rules/quality-tiers.md

Note: No root `CLAUDE.md` exists in this repository. Standing instructions are delivered via `.claude/rules/` with path-scoped frontmatter.

Key constraints relevant to this remediation:
- File-size cap: no production/test/script file may exceed 500 lines.
- Uniform coverage: line >= 85%, branch >= 75% across all tiers (T1-T4); no regression on changed lines.
- COM confinement: Outlook COM interop only in `OpenClaw.MailBridge` (architecture-boundaries rule 1).
- Mandatory toolchain loop, restart-from-format on any failure or file change.
- No new analyzer/nullable suppressions; narrow, justified suppressions only.
- Tier classification: `OpenClaw.Core` T1; `OpenClaw.MailBridge` managed surface T2 / COM-confined T3.
