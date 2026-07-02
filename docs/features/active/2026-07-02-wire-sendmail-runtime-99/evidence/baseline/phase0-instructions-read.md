# Phase 0 — Policy Compliance Reading Evidence

Timestamp: 2026-07-02T10-54
Policy Order: policy-compliance-order skill baseline sequence (CLAUDE.md-loaded rules, then general-code-change, general-unit-test, language/domain-specific rules in scope)

## Files Read

1. `CLAUDE.md`-loaded standing rules (auto-loaded into context):
   - `.claude/rules/general-code-change.md`
   - `.claude/rules/general-unit-test.md`
   - `.claude/rules/quality-tiers.md`
   - `.claude/rules/tonality.md`
   - `.claude/rules/benchmark-baselines.md`
   - `.claude/rules/ci-workflows.md`
   - `.claude/rules/orchestrator-state.md`
2. `.claude/rules/csharp.md` (read explicitly this session)
3. `.claude/rules/architecture-boundaries.md` (read explicitly this session)

## Notes

- `OpenClaw.Core` is T1 per `quality-tiers.yml`; uniform coverage thresholds apply (line >= 85%, branch >= 75%).
- Test suite convention for this solution is MSTest + FluentAssertions + Moq with CsCheck property tests (already referenced), per the approved plan and preflight ALL CLEAR; the generic xUnit/NSubstitute examples in `csharp.md` do not override the established suite convention.
- Toolchain forms per plan: global `csharpier format .` / `csharpier check .`, `dotnet build OpenClaw.MailBridge.sln`, `dotnet test OpenClaw.MailBridge.sln --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`.
