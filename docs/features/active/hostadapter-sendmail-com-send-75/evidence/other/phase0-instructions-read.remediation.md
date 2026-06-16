# Phase 0 — Policy Reads (Remediation)

Timestamp: 2026-06-16T07-57

Policy Order: per `.claude/skills/policy-compliance-order/SKILL.md`, then C# / domain-specific rules.

Files read (in order):
1. `CLAUDE.md` (standing instructions, auto-loaded into context)
2. `.claude/rules/general-code-change.md` (cross-language code change policy; File Size Limit 500 lines)
3. `.claude/rules/general-unit-test.md` (cross-language unit test policy; coverage line >= 85% / branch >= 75%)
4. `.claude/rules/csharp.md` (C# toolchain: CSharpier global tool, .NET SDK analyzers, nullable, MSTest + Moq + FluentAssertions)
5. `.claude/rules/quality-tiers.md` (T1–T4 tiers; uniform coverage thresholds)
6. `.claude/rules/architecture-boundaries.md` (project-reference graph; COM confinement to OpenClaw.MailBridge)
7. `.claude/rules/benchmark-baselines.md`, `.claude/rules/ci-workflows.md`, `.claude/rules/tonality.md` (loaded via project instructions context)

Key constraints applied to this remediation:
- File Size Limit: no production, test, or reusable script file may exceed 500 lines (test code not exempt). Drives R-1.
- CSharpier is invoked as the global tool form (`csharpier format .` / `csharpier check .`); no local dotnet-tool manifest in this repo.
- Coverage uniform thresholds: line >= 85%, branch >= 75%; no regression on changed lines.
- Do not add new `[ExcludeFromCodeCoverage]`, `#pragma warning`, `SuppressMessage`, or `#nullable disable`.
- Do not modify `.claude/rules/` or `.github/instructions/`.
- COM confinement and contract surface unchanged; remediation touches only test files and evidence.
