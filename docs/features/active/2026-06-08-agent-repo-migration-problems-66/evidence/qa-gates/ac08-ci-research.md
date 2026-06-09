# AC-08 — docs/ci.research.md and quality-tiers.md Citation (Issue #66)

Timestamp: 2026-06-08T09-50
Command: `Test-Path docs/ci.research.md`; `rg -n "^## 1" docs/ci.research.md`; `rg -n "ci.research.md" .claude/rules/quality-tiers.md`
EXIT_CODE: 0

Output Summary: AC-08 PASS.

- `docs/ci.research.md` = exists True, with a section 1 heading: `## 1. Module Rigor Tiers (T1–T4)`.
- `.claude/rules/quality-tiers.md:9` cites `docs/ci.research.md` section 1 as the tier source of truth;
  the citation resolves to the now-present file (no dangling reference).
- The section describes the T1–T4 tier system with representative OpenClaw examples per tier and the
  test-project classification rule, consistent with `quality-tiers.yml`.
