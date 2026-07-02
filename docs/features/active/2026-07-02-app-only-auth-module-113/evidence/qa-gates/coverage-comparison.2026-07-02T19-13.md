# Coverage Comparison — Baseline vs Post-Change (P5-T5)

Timestamp: 2026-07-02T19-13

Comparison inputs:
- Baseline Cobertura (OpenClaw.Core.Tests run): artifacts/csharp/baseline-113/506b1420-beba-4450-8d5c-e8cc577e0476/coverage.cobertura.xml
- Post-change Cobertura (OpenClaw.Core.Tests run): artifacts/csharp/postchange-113/d23b82bd-3144-4e2e-bb89-c0f7b0ea8743/coverage.cobertura.xml
- Baseline pooled inputs additionally: artifacts/csharp/baseline-113/a5d9fb03.../coverage.cobertura.xml, artifacts/csharp/baseline-113/bb5ab1c9.../coverage.cobertura.xml
- Post-change pooled inputs additionally: artifacts/csharp/postchange-113/ebf98c71.../coverage.cobertura.xml, artifacts/csharp/postchange-113/002c737a.../coverage.cobertura.xml

## Headline numbers

| Metric | Baseline | Post-change | Threshold | Result |
|---|---|---|---|---|
| Core.Tests run line coverage | 91.00% (1761/1935) | 91.58% (1894/2068) | >= 85% | PASS (improved) |
| Core.Tests run branch coverage | 80.96% (421/520) | 82.06% (453/552) | >= 75% | PASS (improved) |
| Pooled line coverage (3 runs) | 91.02% (4407/4842) | 91.26% (4540/4975) | >= 85% | PASS (improved) |
| Pooled branch coverage (3 runs) | 80.90% (1008/1246) | 81.38% (1040/1278) | >= 75% | PASS (improved) |

No-regression on changed lines: the production diff consists entirely of new files (plus one csproj line, not coverable); no pre-existing covered line was modified, and overall coverage increased, so no changed-line regression exists.

## Per-file new-code coverage (nine src/OpenClaw.Core/CloudAuth files, post-change Cobertura)

| File | Line coverage | Branch coverage |
|---|---|---|
| AppAccessToken.cs | 1/1 (100%) | n/a (no branches) |
| ClientCredentialsTokenProvider.cs | 37/37 (100%) | 4/4 (100%) |
| CloudAuthOptionsValidator.cs | 45/45 (100%) | 24/24 (100%) |
| CloudAuthServiceCollectionExtensions.cs | 20/20 (100%) | n/a (no branches) |
| CredentialFactory.cs | 20/20 (100%) | 2/2 (100%) |
| TokenAcquisitionException.cs | 9/9 (100%) | n/a (no branches) |
| TokenFreshness.cs | 1/1 (100%) | 2/2 (100%) |
| IAppTokenProvider.cs | interface-only, no instrumented lines | n/a |
| CloudAuthOptions.cs | no instrumented lines (auto-property options bag; property bodies are compiler-generated and excluded by `mailbridge.runsettings` `ExcludeByAttribute=CompilerGeneratedAttribute`) | n/a |

Every instrumented added production line is covered (100% per file). `IAppTokenProvider.cs` reports as interface-only per the C# coverage clarification in `.claude/rules/csharp.md`; `CloudAuthOptions.cs` contains no executable behavior beyond compiler-generated auto-property accessors (its binding and defaults are behaviorally verified by `CloudAuthServiceCollectionExtensionsTests` binding/default assertions).

## Outcome

PASS — post-change line coverage 91.58% (Core.Tests run) / 91.26% (pooled) >= 85%; branch coverage 82.06% / 81.38% >= 75%; both improved relative to baseline; all instrumented new production code fully covered.
