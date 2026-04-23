# Phase 0 — Policy Reading Confirmation

Timestamp: 2026-04-22T23-20

## Policy Order

The following policy files were read in the order mandated by the `policy-compliance-order` skill. Files marked `not present` were checked and do not exist in this repository.

1. `CLAUDE.md` — not present at repository root. Confirmed via `ls CLAUDE.md` returning non-zero exit.
2. `.claude/rules/general-code-change.md` — read in full. Cross-language code change policy.
3. `.claude/rules/general-unit-test.md` — read in full. Cross-language unit test policy.
4. `.claude/rules/csharp.md` — read in full. C# toolchain (CSharpier, analyzers, nullable, MSTest+Moq+FluentAssertions). Coverage thresholds: repo-wide >= 80%, new/changed modules >= 90%.
5. `.claude/rules/tonality.md` — read in full. Professional tone, no humor, no hyperbole, restricted metaphors, evidence-first wording.

## Key Constraints Absorbed

- Toolchain order: format (CSharpier) -> lint (analyzers) -> type-check (nullable) -> test (MSTest). Restart from step 1 on any failure or file modification.
- Coverage gates: repo >= 80%, new/changed C# module >= 90%. No waivers.
- File size limit 500 lines for production/test/reusable script files (markdown exempt).
- No temporary files in tests. No external dependencies in unit tests.
- Fail-fast error handling. Nullable reference types enabled and treated as errors.
- Public API changes must remain compatible; EventDto change is additive-only per plan invariants.
- Professional, measured tone in all written output.

EXIT_CODE: 0
