# Current Test Stack Audit

- **Task:** P1-T1
- **Timestamp:** 2026-04-05T21-24
- **Feature:** wrong-target-environment (Issue #4)
- **Source:** `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`

## Current Package References

```xml
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
<PackageReference Include="NUnit" Version="3.14.0" />
<PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
```

## Observations

- Uses NUnit 3.14.0 + NUnit3TestAdapter 4.5.0 — these must be replaced with MSTest packages.
- `Microsoft.NET.Test.Sdk` 17.10.0 — must be retained (required by MSTest too).
- `FluentAssertions` 6.12.0 — must be retained (desired by C# unit test policy).
- Target framework: `net10.0-windows` — already correct; must not change.
- No `coverlet.collector` present — nothing to remove on that front.
- No `NUnit.Analyzers` present — nothing to remove on that front.

## Required Migration

Replace:
```xml
<PackageReference Include="NUnit" Version="3.14.0" />
<PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
```

Add:
```xml
<PackageReference Include="MSTest.TestAdapter" Version="3.6.4" />
<PackageReference Include="MSTest.TestFramework" Version="3.6.4" />
```
