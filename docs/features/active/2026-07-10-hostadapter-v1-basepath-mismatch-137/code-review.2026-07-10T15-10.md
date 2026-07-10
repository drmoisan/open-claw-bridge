# Code Review — Issue #137 (hostadapter-v1-basepath-mismatch)

- Reviewed: 2026-07-10T15-10
- Base: `main` @ `4ce19d186e98c2697eee07ba8a7866ce10af08a0`
- Head: `bug/hostadapter-v1-basepath-mismatch-137` @ `b33db1867ce009a79a77df7e92363c86821ea764`

## Summary of Change

A one-word (`/v1`) literal is stripped from six consumer-side default locations for `OpenClaw__HostAdapter__BaseUrl`, with two new regression tests pinning the corrected value. No new logic, branching, or public API surface is introduced.

## Files Reviewed

| File | Change | Assessment |
|---|---|---|
| `.env.example` | `4319/v1` → `4319` | Minimal, correct. |
| `docker-compose.yml` (x2) | `4319/v1` → `4319` (both occurrences) | Minimal, correct; both occurrences addressed. |
| `docker-compose.dev.yml` | `4319/v1` → `4319` | Minimal, correct. |
| `src/OpenClaw.Core/Program.cs` | `"http://host.docker.internal:4319/v1/"` → `"http://host.docker.internal:4319/"` | Trailing-slash convention preserved (matches `EnsureTrailingSlash` invariant used elsewhere in the same file); consistent with `CoreOptions.cs`'s already-correct class-level default. |
| `scripts/Install.Preflight.psm1` | `'http://host.docker.internal:4319/v1'` → `'http://host.docker.internal:4319'` | Minimal, correct; no signature change to `Get-HostAdapterPreflightUri`. |
| `tests/scripts/Install.Preflight.Tests.ps1` | New `Describe`/`It` block | See Test Quality below. |
| `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs` | New file | See Test Quality below. |

## Design Principles (general-code-change.md)

- **Simplicity first**: the fix is the simplest possible correction — a literal-value edit with no new abstraction, no new class, no new parameter. Appropriate for the defect's scope.
- **Reusability**: N/A — no duplicated logic introduced.
- **Extensibility**: N/A — no public API touched; `Get-HostAdapterPreflightUri`'s signature is unchanged.
- **Separation of concerns**: preserved; the fix does not blur config/I/O/domain boundaries.
- **Non-goals honored**: `CoreOptions.cs` and `src/OpenClaw.HostAdapter/Program.cs` are confirmed byte-identical (`git diff` empty for both), and `HostAdapterHttpClientTests.cs` is confirmed unchanged and still passes 19/19 — all three matching the spec's explicit non-goals.

## Test Quality

### `tests/scripts/Install.Preflight.Tests.ps1`

```powershell
Describe 'Get-HostAdapterPreflightUri default base URL' {
    BeforeAll {
        $script:PreflightPath = Join-Path $PSScriptRoot '..\..\scripts\Install.Preflight.psm1'
        Import-Module $script:PreflightPath -Force
    }

    It 'resolves the default (no OpenClaw__HostAdapter__BaseUrl key in EnvMap) to a URI with no /v1 segment (issue #137)' {
        $uri = Get-HostAdapterPreflightUri -EnvMap @{}
        $uri.AbsolutePath | Should -Not -Match '/v1'
    }
}
```

- Follows the file's existing `Import-Module -Force` pattern; one behavior per `It`, single assertion.
- Independence/isolation: the test calls a pure function with no shared/mutable state; can run in any order.
- Determinism: no network, no clock, no external process — fully deterministic.
- Assertion targets absence of `/v1` rather than presence of a specific replacement string, which is intentionally future-proof against a host/port literal change, per the spec's stated design rationale. This is a reasonable choice, though it does mean a regression that reintroduces `/v1` anywhere else in the resolved URI path (not just as a `/v1` segment) would also be caught, which is the desired behavior.
- Test name is descriptive and references the issue number, aiding traceability.

### `tests/OpenClaw.Core.Tests/CoreHostAdapterBaseUrlFallbackTests.cs`

```csharp
[TestClass]
public sealed class CoreHostAdapterBaseUrlFallbackTests
{
    [TestMethod]
    public void BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment()
    {
        using var factory = new CoreTestWebApplicationFactory(null);
        using var blankBaseUrlFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["OpenClaw:HostAdapter:BaseUrl"] = "" }
                );
            });
        });

        var options = blankBaseUrlFactory.Services.GetRequiredService<IOptions<OpenClawOptions>>();
        var resolvedBaseUrl = options.Value.HostAdapter.BaseUrl;

        resolvedBaseUrl.Should().NotContain("/v1");
    }
}
```

- Arrange–Act–Assert structure is clear, though not explicitly commented with `// Arrange`/`// Act`/`// Assert` markers in the body beyond the two present (`// Arrange` and `// Act`/`// Assert` inline comments exist in the actual file). Minor: the file's inline comments are present but terse; acceptable given the test's narrow, single-assertion scope.
- Exercises the actual `PostConfigure` blank-config fallback branch in `Program.cs:16-18` via `WithWebHostBuilder`/`ConfigureAppConfiguration`, which is the correct integration seam — it does not re-implement or mock the fallback logic, so it verifies the real code path.
- Uses `IClassFixture`-style factory pattern (`CoreTestWebApplicationFactory`) consistent with the surrounding test project's convention (verified: `CoreTestWebApplicationFactory.cs` exists and is used by multiple sibling test files, e.g. `CoreStatusTests.cs`, `CoreReadinessTests.cs`).
- FluentAssertions (`Should().NotContain(...)`) is used per the project's testing standard.
- No external dependencies (network, filesystem, real Docker) — deterministic and isolated.
- Correctly created as a new sibling file rather than extending `HostAdapterHttpClientTests.cs`, which is already at 616 lines (over the 500-line cap); this avoids compounding an existing file-size violation.
- XML doc comment on the class explains why it is a new sibling file and cites the specific lines in `Program.cs` under test — good traceability.
- Minor style note: the test method name `BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment` uses `PascalCase_With_Underscore` framing which is a common MSTest convention in this codebase and matches sibling test naming elsewhere in the project; consistent, not a deviation.

### Regression evidence quality

The branch captured explicit red (`evidence/regression-testing/{ps,csharp}-expect-fail.*.md`) and green (`evidence/regression-testing/{ps,csharp}-post-fix-pass.*.md`) evidence for both new tests, including the actual assertion-failure message text before the fix (e.g., `Did not expect resolvedBaseUrl "http://host.docker.internal:4319/v1/" to contain "/v1"`). This is strong, falsifiable evidence that the tests genuinely exercise the defect rather than being tautological.

## Naming and Style

- All identifiers follow repository convention (`PascalCase` for the C# type/method, `camelCase`/`$baseUrl` for the PowerShell variable already in place, unchanged).
- No abbreviations introduced.
- File-scoped namespace used in the new C# file (`namespace OpenClaw.Core.Tests;`), consistent with the `csharp_style_namespace_declarations = file_scoped:error` requirement.

## Error Handling / Logging

- Not applicable — no new error paths, no new logging statements. The existing `Install.ps1` preflight failure message is unchanged and continues to surface failures clearly, per the spec's explicit statement that no error-handling change is required.

## Dependencies

- No new package dependencies introduced. The new test file uses packages already referenced by `OpenClaw.Core.Tests.csproj` (`FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing`, `MSTest.TestFramework`).

## I/O Boundaries

- Not applicable — this change touches only literal configuration-default values and a corresponding fallback string; no new I/O boundary is introduced or crossed.

## Risks Observed During Review

- None rise to a blocking level. The single residual risk called out by the branch's own evidence (`evidence/other/manual-verification-note.2026-07-10T13-25.md`) — that end-to-end Docker-stage installer behavior has not been exercised live — is explicitly disclosed as an outstanding, non-automatable follow-up and is not part of the AC set defined in `spec.md`. This is a reasonable and transparent scoping decision rather than a gap in the reviewed work.

## Overall Code Quality Verdict

**PASS.** The change is minimal, correctly scoped, well-tested with genuine red/green regression evidence, and respects all stated non-goals (unchanged `CoreOptions.cs`, unchanged `HostAdapter/Program.cs`, unchanged `HostAdapterHttpClientTests.cs`). No design-principle, naming, error-handling, dependency, or I/O-boundary concerns were found.
