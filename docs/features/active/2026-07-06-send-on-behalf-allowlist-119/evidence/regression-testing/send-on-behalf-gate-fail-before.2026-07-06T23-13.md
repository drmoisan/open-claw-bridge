# Send-on-Behalf Gate — Fail-Before / Pass-After Evidence (issue #119, P3-T1 / P3-T3)

## Fail-Before Record (P3-T1, [expect-fail])

Timestamp: 2026-07-06T23-13
Command: `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --no-build --filter "FullyQualifiedName~GraphHostAdapterClientSendMailTests"`
EXIT_CODE: 1

Output Summary:
- Result: Failed! — Failed: 3, Passed: 11, Skipped: 0, Total: 14.
- The three failing tests are the new decisive-deny contract tests, expected to fail
  before the authorization gate exists in `GraphHostAdapterClient.SendMail.cs` (current
  production code has no deny path and drives the strict, no-setup `IAppTokenProvider`
  mock, which throws on the unexpected call):
  - (a) `SendMail_NonAllowlistedPrincipal_DeniesBeforeAnyIo`
  - (b) `SendMail_EmptyAllowlist_DeniesBeforeAnyIo`
  - (c) `SendMail_Denied_MessageNamesKeyAndLogsOneWarningWithRequestIdOnly`
- The allow-path tests pass before the change (existing behavior injects `from` when
  `{p} != {a}` and omits it on self-send), confirming the fail is specific to the deny
  contract:
  - (d) `SendMail_AllowlistedPrincipal_InjectsFromAndSucceeds` — passed
  - (e) `SendMail_CaseDifferingAllowlistEntry_PermitsTheSend` — passed
  - (f) `SendMail_SelfSendEmptyAllowlist_SucceedsWithoutFrom` — passed
- Interpretation: the fail-before demonstrates that, without the gate, a non-allowlisted
  principal (`{p} != {a}`) is silently represented rather than denied.

## Pass-After Record (P3-T3)

Timestamp: 2026-07-06T23-14
Command: `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj --no-build --filter "FullyQualifiedName~GraphHostAdapterClientSendMailTests"`
EXIT_CODE: 0

Output Summary:
- Result: Passed! — Failed: 0, Passed: 14, Skipped: 0, Total: 14.
- After adding the authorization gate to `GraphHostAdapterClient.SendMail.cs`
  (`SendOnBehalfAuthorizer.Authorize` invoked before `executor.ExecuteAsync`, hence
  before token acquisition and any HTTP), the three deny contract tests (a)-(c) now
  pass: the deny envelope returns `UNAUTHORIZED` / `SendOnBehalfDenied` /
  `Retryable == false`, the HTTP handler is invoked zero times, the strict no-setup
  `IAppTokenProvider` mock is never called, the deny message names
  `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` and contains no UPN, and exactly
  one warning log carries the request id only.
- The allow-path tests (d)-(f) and all pre-existing send-mail tests remain green,
  confirming the from-injection predicate and the authorization decision share the
  single `SendOnBehalfAuthorizer` source.
