# hostadapter-sendmail-com-send - Plan

- **Issue:** #75
- **Parent (optional):** Deferred from #70 (cross-cutting Track M + Track H integration; predecessors #71/#72/#73, #74/#76 merged)
- **Owner:** drmoisan
- **Last Updated:** 2026-06-16T06-27
- **Status:** Ready for preflight
- **Version:** 1.0
- **Work Mode:** full-feature
- **Design:** Operator-locked decisions D-A..D-I (below). Additive outbound write path: new `send_mail` MailBridge RPC backed by an Outlook COM send on the STA thread, a Graph-shaped `POST /users/{assistantMailbox}/sendMail` HostAdapter route, and an additive `SendMailAsync` on `IHostAdapterClient`. COM remains confined to `OpenClaw.MailBridge`.

## Required References

- General code change policy: `.claude/rules/general-code-change.md`
- General unit test policy: `.claude/rules/general-unit-test.md`
- C# standards and toolchain: `.claude/rules/csharp.md`
- Architecture boundaries: `.claude/rules/architecture-boundaries.md`
- Module rigor tiers and gate matrix: `.claude/rules/quality-tiers.md`
- Authoritative scope (APPROVED v1.0, 11 ACs): `docs/features/active/hostadapter-sendmail-com-send-75/spec.md`
- Research (file:line for current behavior): `artifacts/research/2026-06-16-issue-75-hostadapter-sendmail-com-send.md`

**All work must comply with these policies; do not duplicate their content here.**

## Locked Decisions (authoritative for this plan; do not re-litigate)

- **D-A:** Success returns HTTP **202 Accepted** with `ApiEnvelope<object?>` `{ ok: true, data: null }`.
- **D-B:** Deserialize the request body via ASP.NET `[FromBody]` minimal-API binding.
- **D-C:** Pass recipients as JSON-serialized arrays per recipient type in CLI/RPC params.
- **D-D:** Do not validate `{assistantMailbox}` against the local profile (single-profile MVP).
- **D-E:** Introduce `IOutlookApplicationProvider` singleton set by `OutlookScanner`, shared with `OutlookComMailSender`.
- **D-F:** Allow empty subject.
- **D-G:** Require >= 1 recipient across To/CC/BCC combined.
- **D-H:** COM send failure -> `BridgeErrorCodes.InternalError` -> HTTP 502 (no new error code).
- **D-I:** Include BCC in MVP via `Recipients.Add(addr).Type = olBCC` (3).
- **Constraints:** local single-profile send; send-on-behalf deferred to PI-1 (the `IOutlookMailSender` seam must accept a future `fromEmailAddress` without breaking callers); additive contract changes only (no major version bump); COM confined to `OpenClaw.MailBridge`.

## Tier Map (from `quality-tiers.yml`)

- `OpenClaw.Core` T1, `OpenClaw.HostAdapter` T1 (tests in `OpenClaw.HostAdapter.Tests` T1, `OpenClaw.Core.Tests` T1).
- `OpenClaw.MailBridge.Contracts` T2, `OpenClaw.HostAdapter.Contracts` T2, `OpenClaw.MailBridge` T2 (tests in `OpenClaw.MailBridge.Tests` T2).
- `OpenClaw.MailBridge.Client` T3.

## Toolchain Gate (C#) — applies to every code/test task and to the final QA phase

Run in this exact order, restarting from step 1 whenever any step fails or rewrites files (restart-on-change loop), until all stages pass in a single uninterrupted pass:

1. **Format** — `csharpier format .` (global CSharpier 1.x subcommand form; the local dotnet-tools manifest is broken, do NOT use `dotnet csharpier`). Per-task format gate uses `csharpier format .`; baseline/final format-check uses `csharpier check .`. Formatter output wins.
2. **Lint / analyzers** — `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` (0 analyzer errors).
3. **Type-check (nullable)** — `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` (0 nullable-flow warnings-as-errors).
4. **Architecture-boundary tests** — `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests"` (NetArchTest assertions) PLUS verification of the `ProjectReference` graph against `.claude/rules/architecture-boundaries.md` Rules 1-7 and the COM-confinement rules (no new edge; `OpenClaw.HostAdapter` does not reference `OpenClaw.MailBridge` or perform COM; `Microsoft.Office.Interop.Outlook` / `System.Runtime.InteropServices` remain only in `OpenClaw.MailBridge`).
5. **Test + coverage** — `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` (Integration-category tests excluded from this command per the filter in the final phase).

Coverage gates (uniform, all tiers): line >= 85%, branch >= 75%; no regression on changed lines. Mutation, property, golden, and contract/schema-snapshot gates are tier-dependent per `quality-tiers.md`; there is no schema-snapshot tool in this repo, so the host-service contract boundary is exercised by the named-pipe `BridgeMethods.All` contract-coverage test and the HTTP envelope round-trip tests in this plan.

## Evidence Locations (canonical, non-overridable)

All evidence is written under `docs/features/active/hostadapter-sendmail-com-send-75/evidence/<kind>/`:

- Baseline: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/baseline/`
- QA gates / final QA: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/`
- Regression / contract / integration evidence: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/regression-testing/`
- Other (architecture verification, AC mapping, implementer enumeration): `docs/features/active/hostadapter-sendmail-com-send-75/evidence/other/`

Non-canonical paths such as `artifacts/baselines/`, `artifacts/baseline/`, `artifacts/qa/`, `artifacts/qa-gates/`, `artifacts/coverage/`, `artifacts/evidence/`, or `artifacts/regression-testing/` are forbidden for evidence output and must not be used. If any caller supplies such a path, EVIDENCE_LOCATION_OVERRIDE_REJECTED applies and the canonical path above is substituted.

Each command-step evidence artifact MUST include: `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. Baseline and final-QA test artifacts MUST record numeric coverage values (baseline percent and changed/new-code percent).

## Implementation Plan (Atomic Tasks)

### Phase 0 — Baseline Capture

- [x] [P0-T1] Read the policy files in required order and record an evidence artifact at `docs/features/active/hostadapter-sendmail-com-send-75/evidence/other/phase0-instructions-read.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Policy Order:`, and the explicit list of files read: `.claude/rules/general-code-change.md`, `.claude/rules/general-unit-test.md`, `.claude/rules/csharp.md`, `.claude/rules/architecture-boundaries.md`, `.claude/rules/quality-tiers.md` (there is no `CLAUDE.md` in this repo; `.claude/rules/` is authoritative).
- [x] [P0-T2] Capture the baseline format state by running `csharpier check .` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/baseline/format.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command: csharpier check .`, `EXIT_CODE:`, `Output Summary:` (clean/dirty file count).
- [x] [P0-T3] Capture the baseline build/analyzer state by running `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/baseline/build-lint.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` (warning/error counts).
- [x] [P0-T4] Capture the baseline nullable type-check state by running `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/baseline/typecheck.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P0-T5] Capture the baseline architecture state by running the NetArchTest boundary tests (`dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests"`) and inspecting the `ProjectReference` graph against `.claude/rules/architecture-boundaries.md`; write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/baseline/architecture.md`.
  - Acceptance: Artifact records the current edges for `OpenClaw.MailBridge`, `OpenClaw.MailBridge.Client`, `OpenClaw.HostAdapter`, `OpenClaw.HostAdapter.Contracts`, `OpenClaw.Core`, and confirms COM types live only in `OpenClaw.MailBridge`; includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` confirming no pre-existing violation.
- [x] [P0-T6] Capture the baseline test + coverage state by running `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/baseline/test-coverage.md`.
  - Acceptance: Artifact includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` with numeric baseline line% and branch% headline values and the total passing test count.

### Phase 1 — MailBridge RPC contract (`send_mail` method registration)

- [x] [P1-T1] Add `public const string SendMail = "send_mail";` to `BridgeMethods` and include it in the `All` set in `src/OpenClaw.MailBridge.Contracts/Models/BridgeContracts.cs`, preserving every existing method constant and `All` entry. (AC-05)
  - Acceptance: File compiles; `BridgeMethods.All` contains `"send_mail"`; no existing method constant or `All` entry is removed or altered; change is additive (no major version bump).
- [x] [P1-T2] Add a contract-coverage test asserting `BridgeMethods.All.Contains("send_mail")` to the existing `BridgeMethods.All` coverage test file `tests/OpenClaw.MailBridge.Tests/BridgeContractsCoverageTests.cs` (follow the existing assertion style; if the file does not exist, create `tests/OpenClaw.MailBridge.Tests/BridgeContractsCoverageTests.cs` with a single `[TestClass]`/`[TestMethod]`). (AC-05, AC-10)
  - Acceptance: Test asserts `BridgeMethods.All.Contains(BridgeMethods.SendMail)` is true and that `BridgeMethods.SendMail == "send_mail"`; test passes; no temporary files.
- [x] [P1-T3] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) with restart-on-change semantics and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase1-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:` per stage, `EXIT_CODE:`, and `Output Summary:`.

### Phase 2 — HostAdapter contract DTOs and `IHostAdapterClient.SendMailAsync`

- [x] [P2-T1] Create `src/OpenClaw.HostAdapter.Contracts/MailContracts.cs` (namespace `OpenClaw.HostAdapter.Contracts`, file-scoped) defining the Graph-aligned `sealed record` DTOs with XML docs: `SendMailRequest(SendMailMessageDto Message, bool SaveToSentItems = true)`; `SendMailMessageDto(string Subject, SendMailBodyDto Body, IReadOnlyList<SendMailRecipientDto> ToRecipients, IReadOnlyList<SendMailRecipientDto>? CcRecipients = null, IReadOnlyList<SendMailRecipientDto>? BccRecipients = null)`; `SendMailBodyDto(string ContentType, string Content)`; `SendMailRecipientDto(SendMailEmailAddressDto EmailAddress)`; `SendMailEmailAddressDto(string Address, string? Name = null)`. (AC-01)
  - Acceptance: File compiles; all types are `sealed record`; uses only BCL types and `OpenClaw.HostAdapter.Contracts`/`OpenClaw.MailBridge.Contracts` references; no `using OpenClaw.Core` and no COM/Interop using; file stays under 500 lines.
- [x] [P2-T2] Add `Task<ApiEnvelope<object?>> SendMailAsync(SendMailRequest request, string? requestId = null, CancellationToken cancellationToken = default)` to `src/OpenClaw.HostAdapter.Contracts/IHostAdapterClient.cs` with XML docs describing `POST /users/{assistantMailbox}/sendMail`, the 202-on-success semantics (D-A), the empty `data: null` payload, and the documented PI-1 send-on-behalf deferral note (AC-09: a future `fromEmailAddress` is added at the bridge seam without breaking this signature). (AC-01, AC-04, AC-09)
  - Acceptance: Interface compiles; method is additive (no existing member changed); parameter order is domain-input first, then `requestId`, then `cancellationToken`, matching the existing method style.
- [x] [P2-T3] Build the solution to enumerate every implementer/mock that now lacks `SendMailAsync` and record the list in `docs/features/active/hostadapter-sendmail-com-send-75/evidence/other/phase2-implementers.md`.
  - Acceptance: Artifact enumerates `HostAdapterHttpClient` (implemented in Phase 5) and any `Mock<IHostAdapterClient>` / fake implementations in test projects that must add the member; includes `Timestamp:`, `Command:`, `EXIT_CODE:` (expected non-zero until Phase 5), `Output Summary:`. Documentation-only; the build is not required to pass at this task.
- [x] [P2-T4] Add a JSON round-trip test for `SendMailRequest` (and nested DTOs, including a BCC-only message and a default `SaveToSentItems`) to a new file `tests/OpenClaw.HostAdapter.Tests/MailContractsTests.cs` (or the nearest existing contracts-test file if one is the established home), asserting field names serialize to the Graph-aligned camelCase shape (`message`, `subject`, `body.contentType`, `toRecipients[].emailAddress.address`, `saveToSentItems`) and round-trip preserves values. (AC-01, AC-10)
  - Acceptance: Test passes using `System.Text.Json` with the Web defaults the adapter uses; asserts `SaveToSentItems` defaults to `true` when omitted from JSON; no temporary files.
- [x] [P2-T5] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase2-toolchain.md`.
  - Acceptance: The build intentionally fails until Phase 5 only if a `Mock<IHostAdapterClient>` requires the new member; if so, add a minimal interface stub to the affected test double in this task to keep the solution compiling, OR record the expected break and proceed (documented). Final acceptance for this artifact: format/lint/nullable/architecture pass and the contracts and `MailContractsTests` compile and pass; `Timestamp:`, `Command:` per stage, `EXIT_CODE:`, `Output Summary:` recorded.

### Phase 3 — MailBridge Outlook application provider seam (D-E)

- [x] [P3-T1] Create `src/OpenClaw.MailBridge/IOutlookApplicationProvider.cs` defining `internal interface IOutlookApplicationProvider { object? Application { get; } void Set(object? application); }` (or the minimal members needed for set-on-connect / clear-on-disconnect), with XML docs stating COM confinement. (AC-06)
  - Acceptance: File compiles; interface is `internal`; lives in `OpenClaw.MailBridge` (not in any Contracts project); no COM type leaks across the assembly boundary; file under 500 lines.
- [x] [P3-T2] Create `src/OpenClaw.MailBridge/OutlookApplicationProvider.cs` implementing `IOutlookApplicationProvider` as a singleton holding `object? Application`, set by `OutlookScanner` on connect and cleared on disconnect, thread-safe for read by the STA send path. (AC-06)
  - Acceptance: File compiles; `Set(null)` clears the reference; default `Application` is `null`; no live COM call is made by the provider itself; file under 500 lines.
- [x] [P3-T3] Inject `IOutlookApplicationProvider` into `src/OpenClaw.MailBridge/OutlookScanner.cs` and set `Application` when `EnsureOutlook()` connects and clear it on the disconnect/teardown path, leaving the existing private `_outlookApp` behavior intact (the provider mirrors the same reference). (AC-06)
  - Acceptance: `OutlookScanner` compiles; on successful `EnsureOutlook()` the provider holds the same `Application` object; on disconnect the provider is cleared; no behavioral change to existing scan paths; file stays under 500 lines.
- [x] [P3-T4] Add unit tests for `OutlookApplicationProvider` to a new file `tests/OpenClaw.MailBridge.Tests/OutlookApplicationProviderTests.cs` covering: default is null; `Set(obj)` then read returns the same reference; `Set(null)` clears. (AC-06, AC-10)
  - Acceptance: Tests pass without live COM (use a plain `object` sentinel as the stand-in application); no temporary files.
- [x] [P3-T5] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase3-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; the architecture stage confirms COM types remain confined to `OpenClaw.MailBridge`; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

### Phase 4 — MailBridge `IOutlookMailSender` seam and COM send implementation

- [x] [P4-T1] Create `src/OpenClaw.MailBridge/IOutlookMailSender.cs` defining `internal interface IOutlookMailSender { Task SendMailAsync(SendMailComRequest request, CancellationToken cancellationToken); }` and an internal plain-data `sealed record SendMailComRequest` carrying the flattened fields (`string Subject`, `string BodyContentType`, `string BodyContent`, `IReadOnlyList<string> To`, `IReadOnlyList<string> Cc`, `IReadOnlyList<string> Bcc`, `bool SaveToSentItems`, and an optional `string? FromEmailAddress = null` reserved for PI-1 send-on-behalf). (AC-06, AC-09)
  - Acceptance: File compiles; interface and record are `internal`; the optional `FromEmailAddress` param/field is present and defaulted so a future caller can supply it without breaking the seam (AC-09); no COM type appears in the seam signature; file under 500 lines.
- [x] [P4-T2] Create `src/OpenClaw.MailBridge/OutlookComMailSender.cs` implementing `IOutlookMailSender`, holding `IOutlookStaExecutor` and `IOutlookApplicationProvider`, executing the COM send inside `sta.InvokeAsync(...)`: late-bind `Application.CreateItem(olMailItem=0)` -> set `Subject` -> set `HTMLBody` when `BodyContentType` equals `HTML` (case-insensitive) else `Body` -> add To/CC via `Recipients.Add(addr)` and BCC via `Recipients.Add(addr).Type = olBCC (3)` (D-I) -> set `DeleteAfterSubmit = !SaveToSentItems` (AC-08) -> call `Send()` -> release every COM object via `com.ReleaseAll(...)` in a `finally`. COM failures propagate (fail-fast) to the caller. Live-COM-only members carry `[ExcludeFromCodeCoverage]` with an in-code comment that each is covered by the Phase 8 integration test. (AC-06, AC-07, AC-08, AC-11)
  - Acceptance: File compiles; COM is confined to `OpenClaw.MailBridge`; every obtained COM wrapper is released in `finally` on success and on exception; `DeleteAfterSubmit = !SaveToSentItems` mapping is present; only live-COM-only members carry `[ExcludeFromCodeCoverage]`, each annotated as integration-test-covered; file stays under 500 lines.
- [x] [P4-T3] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase4-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; architecture stage confirms COM confinement; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`. Coverage of `OutlookComMailSender` is satisfied by `[ExcludeFromCodeCoverage]` on live-COM-only members (validated by the Phase 8 integration test), so the non-excluded surface must still meet thresholds.

### Phase 5 — MailBridge dispatch (`HandleSendMailAsync`) and DI registration

- [x] [P5-T1] Add a `BridgeMethods.SendMail` arm to the dispatch switch in `src/OpenClaw.MailBridge/PipeRpcWorker.cs` (`Handle`, ~lines 201-226) that calls a new private `HandleSendMailAsync`, parsing the flat string params `--subject`, `--body-content-type`, `--body-content`, `--to-recipients` (JSON), `--cc-recipients` (JSON), `--bcc-recipients` (JSON), `--save-to-sent-items` (`true|false`), validating per D-F/D-G/D-H (empty subject allowed; >= 1 recipient across To/CC/BCC; `contentType` in {Text,HTML} case-insensitive), throwing the existing `InvalidRequestException` for validation failures (-> `BridgeErrorCodes.InvalidRequest`), invoking `IOutlookMailSender.SendMailAsync`, mapping a COM/send exception to `RpcResponse.Failure(..., BridgeErrorCodes.InternalError, ex.Message)` (D-H), and returning `RpcResponse.Success(req.Id, null)` on success. Log send attempt and outcome at `info`/`error` per the project logging pattern. (AC-05, AC-06, AC-07, AC-08, AC-11)
  - Acceptance: `PipeRpcWorker.cs` compiles and stays under 500 lines; if adding the handler would exceed the cap, extract a `SendMailRequestValidator` (and/or `SendMailRpcHandler`) helper into a new file `src/OpenClaw.MailBridge/SendMailRpcHandler.cs` (under 500 lines) and call it from `PipeRpcWorker`; the JSON recipient params are deserialized to address lists; default `--save-to-sent-items` to `true` when absent (AC-08).
- [x] [P5-T2] Register `IOutlookApplicationProvider` (singleton), `IOutlookMailSender` -> `OutlookComMailSender`, and any new helper in DI in `src/OpenClaw.MailBridge/BridgeApplication.cs`, and ensure `PipeRpcWorker` receives `IOutlookMailSender` through its constructor. (AC-06)
  - Acceptance: DI graph resolves; `OutlookScanner` and `OutlookComMailSender` share the same `IOutlookApplicationProvider` singleton instance; existing registrations unchanged; file stays under 500 lines.
- [x] [P5-T3] Add a `FakeOutlookMailSender` test double (records the received `SendMailComRequest`; returns `Task.CompletedTask` on the success path; throws `InvalidOperationException` on the failure path) to the MailBridge test doubles file (extend `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTestDoubles.cs`). (AC-10)
  - Acceptance: The fake compiles, implements `IOutlookMailSender`, and exposes the captured request and a configurable throw flag; no live COM.
- [x] [P5-T4] Add MailBridge RPC-dispatch unit tests to a new partial `tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.SendMail.cs` (or `MailBridgeRuntimeTests.Pipe.cs` if that is the established home) using `FakeOutlookMailSender`: (a) valid `send_mail` params dispatch to the sender and return `RpcResponse.Success`; (b) sender throws -> `BridgeErrorCodes.InternalError`; (c) no recipients -> `BridgeErrorCodes.InvalidRequest`; (d) invalid `contentType` -> `InvalidRequest`; (e) empty subject is accepted (D-F); (f) `--save-to-sent-items` absent defaults to `true` and maps to the request. (AC-07, AC-08, AC-10)
  - Acceptance: All cases pass; the fake captures the parsed `SendMailComRequest` and the assertions check subject, body content-type, recipient lists, and `SaveToSentItems`; no temporary files; no live COM.
- [x] [P5-T5] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase5-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; confirm `PipeRpcWorker.cs` and any extracted helper are under 500 lines; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including changed-code line%/branch%.

### Phase 6 — MailBridge Client `send-mail` command arm

- [x] [P6-T1] Add a `"send-mail"` arm to the `Build` switch in `src/OpenClaw.MailBridge.Client/Program.cs` (~lines 127-153) that maps the `--subject`, `--body-content-type`, `--body-content`, `--to-recipients`, `--cc-recipients`, `--bcc-recipients`, `--save-to-sent-items` CLI options into an `RpcRequest` with `Method = "send_mail"` and the corresponding flat string params (recipients passed through as JSON strings per D-C). (AC-05)
  - Acceptance: Client compiles; the new arm is additive (no existing arm changed); recipient JSON strings are forwarded verbatim to the RPC params; file stays under 500 lines.
- [x] [P6-T2] Add or extend a client `Build`-switch unit test asserting the `"send-mail"` arm produces an `RpcRequest` with `Method == "send_mail"` and the expected param keys/values (use the existing client test file convention; if none exists for the `Build` switch, add `tests/OpenClaw.MailBridge.Tests/...` only if the client is tested there, otherwise place the test in the established client test location). (AC-05, AC-10)
  - Acceptance: Test passes and asserts the method name and the flat param mapping including the JSON recipient strings; no temporary files.
- [x] [P6-T3] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase6-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

### Phase 7 — Core `HostAdapterHttpClient.SendMailAsync` (POST path)

- [x] [P7-T1] Add a private `PostAsync<TBody, TResponse>(string relativePath, TBody body, string? requestId, CancellationToken)` helper to `src/OpenClaw.Core/HostAdapterHttpClient.cs` that issues an HTTP `POST` with a JSON body, obtains the token via the existing `TokenReader` seam, and deserializes the `ApiEnvelope<TResponse>` response (mirroring the existing `SendAsync<T>` GET helper, including the missing-token -> `CONFIGURATION_ERROR` short-circuit with no HTTP call). (AC-04)
  - Acceptance: Helper compiles; on a missing token it returns a `CONFIGURATION_ERROR` envelope without issuing an HTTP call; on success it returns the deserialized envelope; matches the existing client style.
- [x] [P7-T2] Implement `SendMailAsync(SendMailRequest request, string? requestId = null, CancellationToken cancellationToken = default)` in `src/OpenClaw.Core/HostAdapterHttpClient.cs` by constructing the relative path `users/{id}/sendMail` (sourcing `{id}` from `options.HostAdapter.MailboxId` via `Uri.EscapeDataString`) and calling `PostAsync<SendMailRequest, object?>(...)`, returning `Task<ApiEnvelope<object?>>`. (AC-01, AC-03, AC-04)
  - Acceptance: Method compiles and returns `ApiEnvelope<object?>`; the wire request is `POST users/{MailboxId}/sendMail` with the JSON `SendMailRequest` body; a 202 response maps to `ok: true`.
- [x] [P7-T3] Add Core client unit tests to a new file `tests/OpenClaw.Core.Tests/HostAdapterHttpClientSendMailTests.cs` using `FakeHttpHandler` and `ConstantTokenReader`: (a) `SendMailAsync` issues a `POST` to `users/me/sendMail`; (b) the request body serializes to the expected Graph-shaped JSON; (c) a 202 response yields `ok: true`, `data: null`; (d) missing token -> `CONFIGURATION_ERROR` with NO HTTP call made (assert the handler recorded zero requests). (AC-03, AC-04, AC-10)
  - Acceptance: All cases pass; the fake handler asserts the HTTP method, path, and serialized body; the missing-token case asserts no outbound request; no temporary files; no real network.
- [x] [P7-T4] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase7-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; the architecture stage confirms `OpenClaw.Core` depends only on `OpenClaw.HostAdapter.Contracts` (Rule 6); artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including changed-code line%/branch%.

### Phase 8 — HostAdapter route (`MailRoutes.cs`), command builder, and 202 success factory

- [x] [P8-T1] Add a 202-Accepted success factory `AcceptedNoContent` (or `Accepted202`) to `src/OpenClaw.HostAdapter/HostAdapterResponses.cs` returning an `AdapterCommandResult<object?>` whose `Envelope` is `ApiEnvelope<object?>` (`ok: true`, `data: null`) and whose `StatusCode` is `202` (D-A). (AC-03)
  - Acceptance: Factory compiles; produces status `202` and `{ ok: true, data: null }`; the existing `Success<T>` factory and 200-path callers are unchanged; file stays under 500 lines.
- [x] [P8-T2] Add `BuildSendMail(...)` to `src/OpenClaw.HostAdapter/HostAdapterCommandBuilder.cs` building a `send-mail` CLI invocation with the flat `--key value` params (`--subject`, `--body-content-type`, `--body-content`, `--to-recipients` JSON, `--cc-recipients` JSON, `--bcc-recipients` JSON, `--save-to-sent-items`), JSON-serializing each recipient list per D-C. (AC-05)
  - Acceptance: Method compiles; produces the exact `send-mail` argument sequence; recipient lists are JSON-serialized arrays of `{address, name}`; default `--save-to-sent-items true` when the request omits it (AC-08); file stays under 500 lines.
- [x] [P8-T3] Create `src/OpenClaw.HostAdapter/MailRoutes.cs` with `public static IEndpointRouteBuilder MapMailRoutes(this IEndpointRouteBuilder app)` registering `POST /users/{assistantMailbox}/sendMail` and a private `HandleSendMailAsync` that: binds the body via `[FromBody] SendMailRequest` (D-B); calls `RequireReadyBridgeAsync` first (409 `BRIDGE_NOT_READY` when not ready); validates >= 1 recipient across To/CC/BCC (D-G), `contentType` in {Text,HTML} case-insensitive, allowing empty subject (D-F); does NOT validate `{assistantMailbox}` (D-D); calls `BuildSendMail` and `IHostAdapterProcessRunner.ExecuteAsync`; returns the 202 factory on success (D-A) and the mapped failure (400 `INVALID_REQUEST`, 502 on runner/COM failure per D-H) otherwise. Document the 64KB pipe-cap (R-1) and offline-Outbox (R-5) caveats in the route XML doc. Preserve the `BearerTokenMiddleware` -> `RequireReadyBridgeAsync` -> validate -> dispatch order. (AC-02, AC-03, AC-07, AC-08)
  - Acceptance: File compiles; route is registered via the extension method (mirroring `SchedulingRoutes.cs`); ordering preserved; file stays under 500 lines.
- [x] [P8-T4] Add the one-line `app.MapMailRoutes();` registration to `src/OpenClaw.HostAdapter/Program.cs` next to the existing `app.MapSchedulingRoutes();` call. (AC-02)
  - Acceptance: `Program.cs` compiles, registers the route, and remains under 500 lines (confirm the line count).
- [x] [P8-T5] Add HostAdapter endpoint unit tests to a new file `tests/OpenClaw.HostAdapter.Tests/HostAdapterSendMailTests.cs` using `HostAdapterTestWebApplicationFactory` + `HostAdapterProcessRunnerStub`: (a) enqueue `status` + `send-mail` stub responses, `POST /users/me/sendMail` with a valid `SendMailRequest` -> **202**, `ok: true`, `data: null`; (b) body with no recipients -> **400** `INVALID_REQUEST` (no `send-mail` invocation); (c) invalid `contentType` -> **400** `INVALID_REQUEST`; (d) bridge not ready -> **409** `BRIDGE_NOT_READY` (no `send-mail` invocation); (e) stub runner returns failure -> **502**. (AC-02, AC-03, AC-07, AC-10)
  - Acceptance: All cases pass and assert the exact HTTP status and envelope shape; negative-path cases assert the `send-mail` runner was not invoked where required; no temporary files; no real network/COM.
- [x] [P8-T6] Add a `BuildSendMail` argument-sequence assertion test to `tests/OpenClaw.HostAdapter.Tests/HostAdapterSendMailTests.cs` (or the established command-builder test file) asserting the produced CLI verb is `send-mail` and the `--key value` sequence (including JSON recipient arrays and `--save-to-sent-items`) matches the specification. (AC-05, AC-10)
  - Acceptance: Test passes and asserts the verb and the full ordered argument list including the JSON-serialized recipient arrays; no temporary files.
- [x] [P8-T7] Run the full C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase8-toolchain.md`.
  - Acceptance: All five stages pass in a single pass; the architecture stage confirms `OpenClaw.HostAdapter` does not reference `OpenClaw.MailBridge` or perform COM (Rule 5); confirm `Program.cs`, `MailRoutes.cs`, and all touched files are under 500 lines; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:` including changed-code line%/branch%.

### Phase 9 — COM send integration tests (real Outlook, gated)

- [x] [P9-T1] Add real-COM integration tests marked `[TestCategory("Integration")]` to a new file `tests/OpenClaw.MailBridge.Tests/OutlookComMailSenderIntegrationTests.cs`, gated behind live-Outlook availability (skip/inconclusive when Outlook is not available, following the repo's existing live-COM gating convention): (a) a valid `SendMailComRequest` with one recipient and a body produces a Sent Items entry (verify by listing Sent Items via COM for the subject after `Send()`); (b) the end-to-end COM send path from `IOutlookMailSender.SendMailAsync` through `MailItem.Send()` completes and releases COM objects. These tests cover the `[ExcludeFromCodeCoverage]` live-COM-only members of `OutlookComMailSender`. (AC-06, AC-10, AC-11)
  - Acceptance: Tests are tagged `[TestCategory("Integration")]`; they run only when live Outlook is available and otherwise report inconclusive/skip without failing the suite; on a live host they assert a Sent Items entry with the expected subject; no temporary files; no `Thread.Sleep`/`Task.Delay`.
- [x] [P9-T2] Execute the integration tests on a live-Outlook host (or record the gated-skip outcome with the gating reason) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/regression-testing/integration-com-send.md`.
  - Acceptance: Artifact records `Timestamp:`, `Command:` (e.g., `dotnet test OpenClaw.MailBridge.sln -c Debug --filter "TestCategory=Integration"`), `EXIT_CODE:`, and `Output Summary:` stating either a PASS with the observed Sent Items entry, or a documented gated-skip (live Outlook unavailable) with `SearchScope:`/`SearchResult:` style notes so the absence is auditable; if skipped, the `[ExcludeFromCodeCoverage]` live-COM members remain covered-by-design pending a live run and this is flagged for the audit.
- [x] [P9-T3] Run the non-integration C# toolchain gate (format -> lint -> nullable -> architecture -> test+coverage with Integration excluded) and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/phase9-toolchain.md`.
  - Acceptance: All five stages pass in a single pass with `--filter "TestCategory!=Integration"` on the test stage; artifact records `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.

### Phase 10 — Documentation

- [x] [P10-T1] Add `POST /users/{assistantMailbox}/sendMail` to the HostAdapter route table in `docs/api-reference.md`, documenting the Graph-shaped `SendMailRequest` body, the **202 Accepted** `{ ok: true, data: null }` success response (D-A), the validation rules (>= 1 recipient, `contentType` in {Text,HTML}, empty subject allowed), and the 64KB pipe-cap (R-1) and offline-Outbox (R-5) caveats. (AC-02, AC-03)
  - Acceptance: `docs/api-reference.md` lists the route consistent with the existing table format and documents the success status, body, and caveats.
- [x] [P10-T2] Update `README.md` HostAdapter surface description to mention the new `sendMail` route and add a note that send-on-behalf (`fromEmailAddress`) is deferred to PI-1. (AC-09)
  - Acceptance: `README.md` references the `sendMail` route and the PI-1 deferral; both docs remain Markdown (exempt from the 500-line cap).

### Phase 11 — Final QA Loop and Acceptance-Criteria Verification

- [x] [P11-T1] Run formatting `csharpier format .` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/final-format.md`.
  - Acceptance: `EXIT_CODE: 0`; artifact includes `Timestamp:`, `Command: csharpier format .`, `Output Summary:`; if files were rewritten, restart the loop from this step.
- [x] [P11-T2] Run lint/analyzers `dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/final-lint.md`.
  - Acceptance: `EXIT_CODE: 0`, 0 analyzer errors, no new suppressions except the documented `[ExcludeFromCodeCoverage]` on live-COM-only members; artifact includes `Timestamp:`, `Command:`, `Output Summary:`.
- [x] [P11-T3] Run nullable type-check `dotnet build OpenClaw.MailBridge.sln -c Debug -p:TreatWarningsAsErrors=true` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/final-typecheck.md`.
  - Acceptance: `EXIT_CODE: 0`, 0 nullable warnings-as-errors, no new nullable suppressions; artifact includes `Timestamp:`, `Command:`, `Output Summary:`.
- [x] [P11-T4] Run the architecture-boundary tests `dotnet test tests/OpenClaw.Core.Tests/OpenClaw.Core.Tests.csproj -c Debug --filter "FullyQualifiedName~AgentArchitectureBoundaryTests"` and verify the `ProjectReference` graph against `.claude/rules/architecture-boundaries.md`; write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/final-architecture.md`.
  - Acceptance: `EXIT_CODE: 0`; artifact confirms no new `ProjectReference` edge; COM types (`Microsoft.Office.Interop.Outlook`, `System.Runtime.InteropServices` COM helpers) remain only in `OpenClaw.MailBridge` (Rule 5/COM-confinement); `OpenClaw.HostAdapter` does not reference `OpenClaw.MailBridge`; `OpenClaw.Core` depends only on `OpenClaw.HostAdapter.Contracts` (Rule 6); includes `Timestamp:`, `Command:`, `EXIT_CODE:`, `Output Summary:`.
- [x] [P11-T5] Run tests with coverage `dotnet test OpenClaw.MailBridge.sln -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage" --filter "TestCategory!=Integration"` and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/final-test-coverage.md`.
  - Acceptance: `EXIT_CODE: 0`, all non-integration tests pass; artifact records numeric post-change line% and branch% and changed/new-code line%/branch%; line >= 85%, branch >= 75%. If any step in P11 rewrote files or failed, restart the loop from P11-T1.
- [x] [P11-T6] Verify the coverage delta and no-regression-on-changed-lines by comparing the baseline (P0-T6) and final (P11-T5) numbers and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/coverage-delta.md`.
  - Acceptance: Artifact reports baseline line%/branch%, post-change line%/branch%, and new/changed-code line%/branch%; confirms no regression on changed lines and that thresholds (line >= 85%, branch >= 75%) hold; confirms the `[ExcludeFromCodeCoverage]` live-COM-only members are accounted for by the Phase 9 integration test. If any required coverage value is unavailable, the outcome is remediation-required (not PASS).
- [x] [P11-T7] Verify each of the eleven acceptance criteria in `spec.md` maps to implementing tasks/tests and write `docs/features/active/hostadapter-sendmail-com-send-75/evidence/other/acceptance-criteria-map.md`.
  - Acceptance: Artifact contains the mapping below, each row citing the satisfying task IDs and test files, with `Timestamp:` and an overall PASS/REMEDIATION-REQUIRED verdict:
    - AC-01 (`SendMailAsync` declared; `SendMail*` DTOs are Graph-aligned `sealed record`s in HostAdapter.Contracts): P2-T1, P2-T2, P2-T4.
    - AC-02 (`MailRoutes.cs` + `app.MapMailRoutes()`; `Program.cs` < 500; middleware->ready->validate->dispatch order preserved): P8-T3, P8-T4, P8-T5.
    - AC-03 (success -> 202 + `{ ok:true, data:null }`): P8-T1, P8-T3, P8-T5, P7-T2, P7-T3.
    - AC-04 (`HostAdapterHttpClient.SendMailAsync` POST to `users/{MailboxId}/sendMail` via `PostAsync`, token via `TokenReader`): P7-T1, P7-T2, P7-T3.
    - AC-05 (`BridgeMethods.SendMail` in `All`; client `send-mail` arm; recipients as JSON arrays): P1-T1, P1-T2, P6-T1, P6-T2, P8-T2, P8-T6.
    - AC-06 (COM send on STA in `OutlookComMailSender`; app via `IOutlookApplicationProvider` set by `OutlookScanner`; To/CC/BCC incl. olBCC; release in finally; COM confined): P3-T1..P3-T3, P4-T1, P4-T2, P5-T2, P9-T1.
    - AC-07 (validation: >= 1 recipient (D-G); contentType {Text,HTML}; empty subject allowed (D-F); `{id}` not validated (D-D); 400 INVALID_REQUEST; COM failure -> InternalError -> 502 (D-H)): P5-T1, P5-T4, P8-T3, P8-T5.
    - AC-08 (`saveToSentItems` defaults true; maps to `DeleteAfterSubmit = !saveToSentItems`): P4-T2, P5-T1, P5-T4, P8-T2.
    - AC-09 (send-on-behalf deferred to PI-1; seam accepts future `fromEmailAddress` without breaking callers; documented): P2-T2, P4-T1, P10-T2.
    - AC-10 (test coverage: integration Sent Items + COM path; endpoint unit test with mocked runner; Core client POST/body/token tests; RPC dispatch fake-sender tests; contract-coverage test for `send_mail`): P1-T2, P5-T3, P5-T4, P7-T3, P8-T5, P8-T6, P9-T1.
    - AC-11 (seven-stage toolchain passes; line >= 85%, branch >= 75%; no regression on changed lines; only documented `[ExcludeFromCodeCoverage]` suppressions covered by integration test; no file > 500 lines; additive contracts only): P11-T1..P11-T6.

## Test Plan

- Unit (HostAdapter, T1): `HostAdapterSendMailTests` (202 success; 400 no-recipients; 400 invalid contentType; 409 bridge-not-ready; 502 runner failure; `BuildSendMail` argument-sequence) via `HostAdapterTestWebApplicationFactory` + `HostAdapterProcessRunnerStub`.
- Unit (MailBridge, T2): `MailBridgeRuntimeTests.SendMail` with `FakeOutlookMailSender` (success; sender-throws -> InternalError; missing/invalid params -> InvalidRequest; empty subject accepted; save-to-sent-items default); `OutlookApplicationProviderTests`; `BridgeContractsCoverageTests` asserting `BridgeMethods.All.Contains("send_mail")`.
- Unit (Core, T1): `HostAdapterHttpClientSendMailTests` with `FakeHttpHandler` + `ConstantTokenReader` (POST to `users/me/sendMail`; body serialization; 202 -> ok:true; missing token -> CONFIGURATION_ERROR with no HTTP call).
- Unit (Client, T3): `Build` switch test asserting the `send-mail` arm maps to `Method = "send_mail"` with the flat params.
- Contract (HostAdapter.Contracts, T2): `MailContractsTests` JSON round-trip and Graph-shape field-name assertions, including BCC-only and default `saveToSentItems`.
- Integration (MailBridge, real COM, gated): `OutlookComMailSenderIntegrationTests` `[TestCategory("Integration")]` — valid request produces a Sent Items entry; validates the COM send path end-to-end; covers the `[ExcludeFromCodeCoverage]` live-COM-only members.
- Determinism: no wall-clock, no `Thread.Sleep`/`Task.Delay`/`Start-Sleep`, no temporary files; all seams (runner, HTTP handler, token reader, mail sender, COM application) are faked/stubbed in unit tests.
- Coverage evidence:
  - Baseline: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/baseline/test-coverage.md`
  - Post-change: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/final-test-coverage.md`
  - Comparison: `docs/features/active/hostadapter-sendmail-com-send-75/evidence/qa-gates/coverage-delta.md`

## Open Questions / Notes

- Sequencing rationale: contracts land before consumers — `BridgeMethods.send_mail` (P1) and the HostAdapter DTOs/interface (P2) precede the COM seams (P3-P4), dispatch (P5), client arm (P6), Core client (P7), and the HTTP route (P8). The integration tests (P9) follow the COM implementation; docs (P10) and the final QA loop (P11) close out.
- File-size watch points: `MailRoutes.cs` is a new extracted file (per the `SchedulingRoutes.cs` precedent) so `Program.cs` stays under 500 lines; `PipeRpcWorker.cs` is ~404 lines today and the `HandleSendMailAsync` arm must keep it under 500 — extract `SendMailRpcHandler.cs` / a validator if needed (P5-T1).
- `[ExcludeFromCodeCoverage]` is applied only to live-COM-only members of `OutlookComMailSender`; each such member must be exercised by the Phase 9 integration test on a live-Outlook host. If the integration run is gated-skipped (no live Outlook), the audit records this as covered-by-design pending a live run (P9-T2).
- All contract changes are additive; no major version bump. No new package dependencies and no new `ProjectReference` edges are introduced.
- The research artifact lives at `artifacts/research/` per the research agent's output convention; this is non-evidence orchestration output and is allowed under `artifacts/research/`.
