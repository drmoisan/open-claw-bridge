# Phase 0 — Policy Instructions Read

- Timestamp: 2026-04-10T23-00
- Policy Order: general-code-change → general-unit-test → csharp-code-change → csharp-unit-test

## Files Read

1. `.github/copilot-instructions.md` — **does not exist** in the repository. Documented as absent; other policy files cover all required rules.
2. `.github/instructions/general-code-change.instructions.md` — read and confirmed. Covers design principles, module structure (500-line limit), error handling, toolchain loop (format → lint → type-check → test).
3. `.github/instructions/general-unit-test.instructions.md` — read and confirmed. Covers independence, isolation, determinism, coverage requirements (>=80% repo-wide, >=90% new code), temp-file prohibition with exception list.
4. `.github/instructions/csharp-code-change.instructions.md` — read and confirmed. Covers csharpier formatting, msbuild analyzers, nullable analysis, C# design principles, internal access modifiers.
5. `.github/instructions/csharp-unit-test.instructions.md` — read and confirmed. Covers MSTest framework, Moq mocking, FluentAssertions, C# toolchain commands.

## Notes

- `.github/copilot-instructions.md` is absent. All required policy content is covered by the remaining four files and the AGENTS.md aggregate.
- All policies are understood and will be enforced during execution.
