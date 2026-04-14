# HTTP 500 vs 503 Assessment — Token File Missing Configuration Error

File: src/OpenClaw.HostAdapter/BearerTokenMiddleware.cs
ApproxLine: 22
CurrentStatus: 500
RecommendedStatus: 503
ChangeComplexity: Simple — the status code is set via `HostAdapterResponses.ConfigurationError<T>()` in `HostAdapterResponses.cs` (line 105), which hard-codes `StatusCodes.Status500InternalServerError`. Changing it to `StatusCodes.Status503ServiceUnavailable` requires a single-line edit in `HostAdapterResponses.cs`. No test currently covers this code path (coverage.cobertura.xml shows `line-rate="0"` for `ConfigurationError`), so no test updates are required for the status-code change itself.
DeferralReason: Per remediation-inputs.2026-04-13T14-00.md Item 3, this change is out of scope for this remediation pass. A follow-up GitHub issue should be opened post-merge.
