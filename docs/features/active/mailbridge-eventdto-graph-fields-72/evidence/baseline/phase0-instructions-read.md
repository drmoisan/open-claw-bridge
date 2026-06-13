# Phase 0 — Policy Instructions Read

Timestamp: 2026-06-13T03-03

Policy Order: Per `.claude/skills/policy-compliance-order`, repository policy files were read in the required precedence order before any execution.

Files read (in order):
1. `CLAUDE.md` (standing instructions; auto-loaded into context)
2. `.claude/rules/general-code-change.md` (cross-language code change policy)
3. `.claude/rules/general-unit-test.md` (cross-language unit test policy)
4. `.claude/rules/csharp.md` (C# standards — language-specific, in scope)
5. `.claude/rules/quality-tiers.md` (T1–T4 tier system; uniform coverage thresholds line >= 85%, branch >= 75%)
6. `.claude/rules/architecture-boundaries.md` (project-graph + COM confinement rules)

Additional in-scope policy rules auto-loaded and observed:
- `.claude/rules/tonality.md`
- `.claude/rules/benchmark-baselines.md` (not applicable; no benchmark baseline touched)
- `.claude/rules/ci-workflows.md` (not applicable; no workflow YAML touched)

Key constraints carried into execution:
- EventDto: append nine new optional parameters AFTER `ResponseStatus = null`; existing positional call sites must compile unmodified.
- Redaction: `ResponseShaper.ShapeEvent` nulls `bodyFull` in safe mode; enhanced mode returns raw untruncated body (NOT through `BodySanitizer.NormalizePreview`).
- 500-line file cap: `OutlookScanner.cs` is pre-existing over-cap (507 lines); new scanner helpers go in new partial `OutlookScanner.GraphFields.cs`. `CoreCacheRepository.cs` is pre-existing over-cap (692 lines); split if it grows materially.
- COM stays only in `OpenClaw.MailBridge`; EventDto + sensitivity helper stay in leaf `OpenClaw.MailBridge.Contracts`.
- Out of scope: `CoreCacheRepository` missing-`response_status` gap (issue #80).
- Tests: MSTest + Moq + FluentAssertions; deterministic; no temp files; no Thread.Sleep/Task.Delay; in-memory/shared-cache SQLite.
- Coverage: line >= 85%, branch >= 75% (T2); no regression on changed lines.
- Evidence canonical scheme only: `docs/features/active/mailbridge-eventdto-graph-fields-72/evidence/<kind>/`.

EXIT_CODE: 0
Output Summary: All required policy files read in order; constraints recorded. No policy edits performed.
