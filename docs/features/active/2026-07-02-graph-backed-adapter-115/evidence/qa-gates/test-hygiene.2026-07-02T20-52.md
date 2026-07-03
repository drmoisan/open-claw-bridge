# QA Gate — Test Hygiene (P8-T2)

Timestamp: 2026-07-02T20-52
Command: grep -n -E "graph\.microsoft\.com|HttpClientHandler|GetTempFileName|GetTempPath|File\.Write|Thread\.Sleep|DateTime\.UtcNow|DateTime\.Now" tests/OpenClaw.Core.Tests/CloudGraph/*.cs ; grep -n "Task\.Delay(" tests/OpenClaw.Core.Tests/CloudGraph/*.cs
EXIT_CODE: 0
Output Summary: PASS — zero live-endpoint or banned-API violations.
- `Task.Delay(`: zero matches in any CloudGraph test file (grep exit 1 = no matches). All time flows through `FakeTimeProvider.Advance` + `Task.Yield`.
- `Thread.Sleep`, `DateTime.UtcNow`, `DateTime.Now`, `HttpClientHandler`, `GetTempFileName`, `GetTempPath`, `File.Write`: zero matches.
- `graph.microsoft.com`: 3 matches, each explained and benign — none is a request target:
  1. `GraphAdapterOptionsValidatorTests.cs:121` — `"http://graph.microsoft.com/v1.0/"` is a negative-path validator input (non-https scheme rejection); no HTTP request is made.
  2. `GraphAdapterOptionsValidatorTests.cs:122` — `"graph.microsoft.com/v1.0/"` is a negative-path validator input (relative-URI rejection); no HTTP request is made.
  3. `GraphServiceCollectionExtensionsTests.cs:89` — assertion pinning the `GraphAdapterOptions.BaseUrl` spec default value; no HTTP request is made.
- All handler-level tests use the `FakeHttpHandler` mock with `BaseAddress = https://graph.example.test/v1.0/`; no live Graph calls and no temp files anywhere in the suite.
