# Phase 0 Baseline 05 — Test Harness Confirmation (Issue #120)

Timestamp: 2026-07-06T23-11

Verified by reading `tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj`,
`tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`, and
`src/OpenClaw.Core/OpenClaw.Core.csproj`.

## Confirmed package references (tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj)

- `MSTest.TestFramework` 3.6.4 and `MSTest.TestAdapter` 3.6.4 (test framework — `[TestClass]`/`[TestMethod]`/`[DataTestMethod]`/`[DataRow]`).
- `Moq` 4.20.72 (test doubles for `IMailboxScopeProbe`, `IAppTokenProvider`).
- `FluentAssertions` 6.12.0 (assertions).
- `CsCheck` 4.7.0 (property-based tests).
- `Microsoft.Extensions.TimeProvider.Testing` 10.6.0 (namespace `Microsoft.Extensions.Time.Testing`, `FakeTimeProvider`).
- `Microsoft.NET.Test.Sdk` 18.4.0, `NetArchTest.Rules` 1.3.2 (architecture-boundary tests), `coverlet.collector` 6.0.2 (coverage), `Microsoft.AspNetCore.Mvc.Testing` 10.0.5.

## FakeHttpHandler fixture

- File path: `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs`.
- Definition: `internal sealed class FakeHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler` (lines 606-616). Namespace `OpenClaw.Core.Tests`. It forwards each outbound request to the caller-supplied delegate, giving full control over HTTP responses with no network access. The Graph error-matrix tests (`GraphRequestExecutorErrorMatrixTests`) already reuse this handler to construct a `GraphRequestExecutor` with a `FakeTimeProvider(Start)` and a `Mock<IAppTokenProvider>(MockBehavior.Strict)`; the Phase 3 `GraphMailboxScopeProbe` tests will follow the same construction.

## InternalsVisibleTo grant

- Location: `src/OpenClaw.Core/OpenClaw.Core.csproj`, lines 8-11, as an
  `<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">` with
  `<_Parameter1>OpenClaw.Core.Tests</_Parameter1>`. This grants the test assembly access to
  the new `internal` ScopeValidation types, so no `public` surface is required.

## Divergence from spec assumptions

- None. All spec assumptions hold: the observed harness is MSTest + Moq + FluentAssertions +
  CsCheck + `FakeTimeProvider` + `FakeHttpHandler` (the recorded divergence from the
  xUnit/NSubstitute wording in `.claude/rules/csharp.md`, per spec Constraints & Risks), and
  the `InternalsVisibleTo("OpenClaw.Core.Tests")` grant is present. Test namespace convention
  is `OpenClaw.Core.Tests.<SubNamespace>` (e.g., `OpenClaw.Core.Tests.CloudGraph`); new tests
  will use `OpenClaw.Core.Tests.ScopeValidation`.

Output Summary: Test harness matches spec assumptions with no divergence. All required
packages, the `FakeHttpHandler` fixture path, and the `InternalsVisibleTo` grant are
confirmed present.
