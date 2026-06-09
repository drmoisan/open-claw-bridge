---
paths:
  - "**/*.cs"
  - "**/*.csproj"
description: C#-specific toolchain and coding standards.
---

# C# Code Standards

This rule file summarizes the C#-specific policies for this repository. It targets the OpenClaw MailBridge solution: a Windows-first **.NET 10** solution that reads Outlook through **COM** on a dedicated STA thread, caches normalized data in SQLite, and exposes it over a named pipe and local HTTP. The toolchain is CSharpier, the .NET SDK analyzers, nullable reference types, and **MSTest + Moq + FluentAssertions**, with uniform coverage thresholds per `.claude/rules/quality-tiers.md`.

## Solution Layout

The solution is `OpenClaw.MailBridge.sln`, targeting `net10.0` and `net10.0-windows` (SDK pinned in `global.json` to `10.0.201`). Projects:

- `OpenClaw.MailBridge.Contracts` — shared DTOs, RPC contracts, error codes, settings, validation/sanitization helpers. Leaf project; depends on no other solution project.
- `OpenClaw.MailBridge` — Windows bridge host: Outlook COM/STA scanning, SQLite cache, named-pipe RPC. **The only project that performs Outlook COM interop.**
- `OpenClaw.MailBridge.Client` — named-pipe client CLI.
- `OpenClaw.HostAdapter.Contracts` — HTTP envelope types and the typed HostAdapter client contract.
- `OpenClaw.HostAdapter` (`Microsoft.NET.Sdk.Web`) — local authenticated HTTP adapter that shells out to the client executable.
- `OpenClaw.Core` (`Microsoft.NET.Sdk.Web`) — local-only ASP.NET Core UI and API with its own SQLite cache.

Architecture boundaries between these projects are defined in `.claude/rules/architecture-boundaries.md`.

## Toolchain

Run the toolchain in order: format → lint/type-check → architecture → test. Restart from step 1 if any step fails or changes files.

1. **Formatting — CSharpier**: format all `*.cs` files with CSharpier. CSharpier is file-based and does not rewrite `*.csproj`. Do **not** use `dotnet format` (it loads the project model and can mishandle projects). Command: `csharpier .` (or `dotnet csharpier .` when installed as a dotnet tool). If a diff disagrees with CSharpier, formatter output wins.
2. **Linting / Static Analysis — .NET SDK analyzers**: C# must pass the built-in Roslyn/.NET SDK analyzer diagnostics produced during build. Prefer fixing diagnostics over suppressing them; keep any suppression narrow and documented in-code. Command: `dotnet build OpenClaw.MailBridge.sln -c Debug`. For a stricter gate, build with analyzer and code-style enforcement: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true`.
3. **Type Checking — Nullable Analysis**: nullable reference types are enabled per project (`<Nullable>enable</Nullable>`). Treat nullable-flow warnings as type-safety errors and fix the root null-state issue rather than suppressing. A stricter local gate: `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true`.
4. **Architecture**: verify the project-reference boundaries in `.claude/rules/architecture-boundaries.md` (compile-time graph plus review today; `NetArchTest.Rules` in a `*.ArchitectureTests` project if/when automated assertions are added).
5. **Testing — MSTest**: `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"`. Coverage is collected by `coverlet.collector` and configured by `mailbridge.runsettings`.

> No third-party analyzer packages, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.globalconfig`, or `BannedSymbols.txt` are currently present in this repository. Statements below that describe enforcement refer to the SDK defaults and to code review unless a tool is named and confirmed present.

## C# Design and Type-Safety Principles

1. **Strong contracts and explicit APIs**: public methods, constructors, and properties express clear contracts. Use explicit types at public boundaries; use `var` only when the type is obvious.
2. **Null-safety by default**: keep nullable reference types enabled; model optional values with nullable annotations and guard clauses; use nullability attributes where they improve flow analysis.
3. **Prefer composition and focused types**: keep classes cohesive and scoped to one responsibility; favor composition over inheritance unless polymorphism is a clear requirement. Prefer immutable `record`/value types for data-centric models (the contract DTOs are `sealed record`s).
4. **Asynchrony and resource safety**: use `async`/`await` for I/O-bound work; prefer `using`/`await using` for disposable resources. COM objects must be released deterministically (see COM Interop below).

## Coding Standards

- **Naming**: `PascalCase` for types and public members. `camelCase` for locals and private fields/parameters; private fields use `_camelCase`. Interfaces use the `I` prefix. Async methods carry the `Async` suffix.
- **File-scoped namespaces**: the repository convention is file-scoped namespaces (for example `namespace OpenClaw.MailBridge.Contracts.Models;`).
- **Exceptions**: fail fast with explicit, specific exceptions when invariants are violated. Avoid broad `catch (Exception)` unless at a defined boundary that re-raises or adds context. Use the project's error-code set (`BridgeErrorCodes`, `ApiError`) for cross-boundary failures rather than leaking raw exceptions.
- **Logging**: use the project's logging pattern; do not use ad-hoc console output in production paths. Log actionable context at appropriate levels.
- **Public surface**: keep the public API surface intentional and minimal; prefer `internal` for non-public APIs.
- **XML docs**: public APIs should include XML documentation comments when behavior or contract is non-obvious.
- **File size**: no file exceeds 500 lines (see `.claude/rules/general-code-change.md`).

## COM Interop (Outlook)

Outlook automation is intrinsic to this solution and is **confined to `OpenClaw.MailBridge`**.

- All Outlook COM calls run on a single dedicated STA thread; do not call Outlook objects from arbitrary threads or the thread pool.
- Keep COM interop behind narrow interfaces/adapters (for example the active-object and scanner seams) so other layers and unit tests never touch live COM.
- Release COM objects deterministically; do not let runtime callable wrappers accumulate.
- Other projects (`Client`, `HostAdapter`, `HostAdapter.Contracts`, `Core`, `Contracts`) must not perform Outlook automation; they consume bridge data through the named-pipe and HTTP contracts.

## Testing Standards

- Use **MSTest** (`Microsoft.VisualStudio.TestTools.UnitTesting`) with `[TestClass]` and `[TestMethod]`. Do not introduce xUnit or NUnit.
- Use **`[DataRow]`** with `[DataTestMethod]` for parameterized tests.
- Use **Moq** for test doubles. Example: `var runner = new Mock<IHostAdapterProcessRunner>(); runner.Setup(r => r.RunAsync(...)).ReturnsAsync(result);`.
- Prefer **FluentAssertions** for assertions; use MSTest `Assert` only when FluentAssertions is not practical for a specific shape.
- Follow Arrange–Act–Assert structure with clear, actionable failure messages.
- No external dependencies (network, real Outlook, live processes) in unit tests; mock the boundary seams. Creation and use of temporary files in tests is prohibited.

### Coverage

- Line coverage `>= 85%` and branch coverage `>= 75%`, uniform across all tiers (T1–T4) per `.claude/rules/quality-tiers.md`. No tier-specific lower floor is used.
- Coverage regression on changed lines is a blocking finding.
- Tier-dependent gates (mutation, property-based, golden tests) are defined in `.claude/rules/quality-tiers.md` and depend on the `quality-tiers.yml` classification. That classification file and the corresponding .NET tooling (for example Stryker.NET for mutation testing) are not yet configured in this repository; add them under the names chosen in `quality-tiers.md` when the gates are enabled.

### Determinism

Per `.claude/rules/general-unit-test.md`, test code must be deterministic:

- Prefer **`TimeProvider`** (injected) for time; tests advance simulated time with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) rather than reading wall-clock time.
- Supply randomness through a seeded seam; print the seed on failure.
- Do not use `Thread.Sleep`, `Task.Delay`, or real wall-clock waits in tests. These are policy requirements; they are not currently enforced by a banned-API analyzer in this repository.

## DI Seams

Introduce the smallest seam that enables reliable unit testing, in this order of preference:

1. **Interface seam (preferred)** — extract boundary calls into narrow, purpose-specific interfaces. Existing examples: `IHostAdapterProcessRunner`, `IHostAdapterTokenProvider`. Keep interfaces minimal.
2. **Injectable delegate seam** — a narrow `Func<>`/`Action<>` for a single call path when a full interface is excessive; the default must remain safe and deterministic.
3. **Adapter seam for static / third-party / COM APIs** — wrap the static, third-party, or COM call behind a small adapter so tests can substitute it with Moq.
4. **Clock seam** — inject `TimeProvider`; tests use `FakeTimeProvider`.

## Prohibited Behaviors

- Broad refactors across unrelated projects or files.
- Introducing heavy generic abstraction frameworks without need.
- Performing Outlook COM interop outside `OpenClaw.MailBridge`.
- Suppressing analyzer or nullable diagnostics instead of fixing the root cause.
- Weakening assertions or relaxing test expectations to make tests pass.
- Adding sleeps, retries, or timing hacks to mask flaky behavior.
- Reporting success without running the required toolchain.
