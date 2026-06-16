# Phase 0 — Policy Instructions Read

Timestamp: 2026-06-13T13-34

Policy Order:
1. CLAUDE.md (standing instructions)
2. .claude/rules/general-code-change.md (cross-language code change policy)
3. .claude/rules/general-unit-test.md (cross-language unit test policy)
4. .claude/rules/csharp.md (C#-specific toolchain and standards)
5. .claude/rules/architecture-boundaries.md (COM confinement + project graph)
6. .claude/rules/quality-tiers.md (T1-T4 rigor + uniform coverage thresholds)

Files Read:
- CLAUDE.md (loaded via standing context)
- .claude/rules/general-code-change.md (loaded via standing context)
- .claude/rules/general-unit-test.md (loaded via standing context)
- .claude/rules/csharp.md (read explicitly this session)
- .claude/rules/architecture-boundaries.md (read explicitly this session)
- .claude/rules/quality-tiers.md (loaded via standing context)

Additional inputs read:
- docs/features/active/mailbridge-messagedto-resolved-fields-73/spec.md (locked decisions D-A..D-D, AC-01..AC-11)
- docs/features/active/mailbridge-messagedto-resolved-fields-73/plan.2026-06-13T13-34.md (plan of record)

Output Summary: All six required policy files were read in the prescribed order. The only language
in scope is C# (.NET 10). COM stays confined to OpenClaw.MailBridge. Uniform coverage thresholds
apply: line >= 85%, branch >= 75%; no regression on changed lines; no new suppressions; no file > 500 lines.
