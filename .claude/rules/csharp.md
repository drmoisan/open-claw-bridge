---
paths:
  - "**/*.cs"
  - "**/*.csproj"
description: C#-specific toolchain and coding standards (No-COM, xUnit).
---

# C# Code Standards

This rule file summarizes the C#-specific policies for this repository. It targets the No-COM .NET foundation: xUnit, NSubstitute, FluentAssertions, `dotnet build`, the analyzer stack, `TimeProvider`, and uniform coverage thresholds.

## Toolchain

1. **Formatting — CSharpier**: All C# source files must be formatted with CSharpier. Do not use `dotnet format`. Command: `dotnet tool restore` followed by `dotnet csharpier check .` (or `dotnet csharpier .` to auto-format).
2. **Linting — .NET Analyzers**: C# code must pass Roslyn/.NET analyzer diagnostics. Analyzer enforcement is centralized in `Directory.Build.props` (`AnalysisLevel=latest-all`, `AnalysisMode=All`, `TreatWarningsAsErrors=true`). Command: `dotnet build` runs analyzers as part of the build.
3. **Type Checking — Nullable Analysis**: Nullable reference types are enabled solution-wide via `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`). Command: `dotnet build` enforces nullable warnings as errors.
4. **Testing — xUnit + NSubstitute + FluentAssertions**: Run tests with: `dotnet test --collect:"XPlat Code Coverage"`.

Run the toolchain in order: format → lint → type-check → architecture → test. Restart from step 1 if any step fails or changes files.

## Coding Standards

- **Naming**: `PascalCase` for types and public members. `camelCase` for locals and private fields/parameters. Private fields use `_camelCase`. Interfaces use the `I` prefix. Async methods carry the `Async` suffix.
- **Null safety**: Keep nullable reference types enabled. Model optional values with nullable annotations and guard clauses.
- **Composition over inheritance**: Keep classes cohesive and scoped to one responsibility. Favor composition unless polymorphism is a clear requirement.
- **Async/await**: Use `async`/`await` for I/O-bound operations. Prefer `using`/`await using` for disposable resources.
- **Exceptions**: Fail fast with explicit exceptions. Avoid broad `catch (Exception)` unless at a defined boundary with added context.
- **Public surface**: Keep public API surface intentional and minimal. Prefer `internal` for non-public APIs.
- **XML docs**: Public APIs should include XML documentation comments when behavior or contract is non-obvious.
- **File-scoped namespaces**: Required (`csharp_style_namespace_declarations = file_scoped:error` in `.editorconfig`).

## Testing Standards

- Use **xUnit** as the test framework with `[Fact]` and `[Theory]` attributes.
- Use **`[Theory]` + `[InlineData]`** for parameterized tests.
- Use **`IClassFixture<T>`** to share expensive setup across tests within a class.
- Use **NSubstitute** for test doubles. Example: `var sut = Substitute.For<IService>(); sut.Get().Returns(value);`.
- Prefer **FluentAssertions** for assertions; use xUnit `Assert` only when FluentAssertions is not practical.
- Follow Arrange–Act–Assert structure.
- No external dependencies in unit tests.

### Coverage

- Line coverage line >= 85% and branch coverage branch >= 75% uniform across all tiers (T1–T4). No tier-specific lower floor is used.
- Mutation score mutation >= 75% on T1 modules (via Stryker.NET).
- Coverage regression on changed lines is a blocking finding.
- Interface-only files with no executable behavior — files consisting solely of `interface` declarations or abstract contracts — may be omitted from coverage measurement. Such files legitimately report 0% executable coverage. This is a clarification only; it does not lower any coverage threshold.

### Property-Based and Mutation Testing

- **CsCheck**: at least one property-based test per pure function on T1 and T2 modules.
- **Stryker.NET**: mutation testing required on T1 modules with a mutation score mutation >= 75%. Runs in pre-merge or nightly pipelines.

### Golden Tests

- **Verify.Xunit**: required for T1 classifier-output modules, tested against a versioned corpus.

## Analyzer Stack

All projects reference the following analyzer packages via `<PackageReference>` with `PrivateAssets="all"` (versions pinned centrally in `Directory.Packages.props`):

- `Meziantou.Analyzer` — `PrivateAssets="all"`
- `SonarAnalyzer.CSharp` — `PrivateAssets="all"`
- `Roslynator.Analyzers` — `PrivateAssets="all"`
- `AsyncFixer` — `PrivateAssets="all"`
- `SecurityCodeScan.VS2019` — `PrivateAssets="all"`
- `Microsoft.CodeAnalysis.BannedApiAnalyzers` — `PrivateAssets="all"`

The shared `<ItemGroup>` lives in `Directory.Build.props` so the stack applies to every project automatically.

## Banned APIs

The following APIs are banned outside an explicit allowlist; enforcement is via `Microsoft.CodeAnalysis.BannedApiAnalyzers` against `BannedSymbols.txt` (at solution root, wired through `Directory.Build.props` as an `<AdditionalFiles>` entry):

- `DateTime.Now` (use `TimeProvider.GetLocalNow()` on an injected `TimeProvider`).
- `DateTime.UtcNow` (use `TimeProvider.GetUtcNow()` on an injected `TimeProvider`).
- `Random.Shared` (inject a seeded `Random` or use a deterministic seam).
- `Thread.Sleep` (banned; use cooperative awaits and fake-time advancement).
- `Task.Delay` (banned in production paths; tests must use `FakeTimeProvider`).

Tests inject `TimeProvider` via `Microsoft.Extensions.TimeProvider.Testing`'s `FakeTimeProvider` rather than calling `DateTime.UtcNow` or `Task.Delay` directly.

## Deterministic Test Rules

Unit tests must not depend on network, mutable machine PATH or profile state, implicit working-directory assumptions, or external services. Use seam-based mocking for all external boundaries (processes, HTTP, filesystem, clocks). Tests must produce identical results in the IDE test runner and in CLI runs so local and CI behavior agree.

## DI Seams

Introduce the smallest seam that enables reliable unit testing. Apply in this order of preference:

1. **Interface seam (preferred)** — extract boundary calls into narrow purpose-specific interfaces (for example, `IProcessRunner`, `IFileSystem`). Keep interfaces minimal.
2. **Injectable delegate seam** — use a narrow `Func<>`/`Action<>` delegate for a single call path when a full interface is excessive. Default behavior must remain safe and deterministic.
3. **Adapter seam for static or third-party APIs** — wrap the static or third-party call behind a small adapter so tests can substitute the adapter with NSubstitute.

### Clock Seam

- **`TimeProvider` is preferred** for new code (since .NET 8). Inject `TimeProvider` and use `GetUtcNow()` / `GetLocalNow()` / `CreateTimer()`.
- Test code injects `FakeTimeProvider` from **`Microsoft.Extensions.TimeProvider.Testing`** to advance simulated time deterministically.
- `IClock` legacy: acceptable only in legacy or pre-.NET 8 contexts that have not yet been migrated. New code must use `TimeProvider`.

## Prohibited Behaviors

- Broad refactors across unrelated projects or files.
- Introducing heavy generic abstraction frameworks without need.
- Creating analyzer debt and deferring cleanup.
- Weakening assertions or relaxing test expectations to make tests pass.
- Adding sleeps, retries, or timing hacks to mask flaky behavior.
- Reporting success without running the required toolchain.
