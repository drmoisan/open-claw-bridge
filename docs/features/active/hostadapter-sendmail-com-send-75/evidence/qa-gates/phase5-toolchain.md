# Phase 5 — Toolchain Gate

Timestamp: 2026-06-16T07-57

Phase 5 adds the `send_mail` dispatch arm to `PipeRpcWorker`, the extracted `SendMailRpcHandler`
parser/validator, DI registration, the `FakeOutlookMailSender` double, and six RPC-dispatch unit
tests. Full-solution stages spanning Core remain on the single expected CS0535 break until P7-T2;
re-verified green at P7-T4 / P11. MailBridge + tests build and pass independently.

## Stage 1 — Format
Command: csharpier format .
EXIT_CODE: 0
Output Summary: Formatted 187 files. No CSharpier changes beyond new/edited files.

## Stage 2+3 — Lint / Analyzers + Nullable (MailBridge + MailBridge.Tests)
Command: dotnet build tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Warning(s), 0 Error(s). The new `IOutlookMailSender? mailSender = null`
optional primary-ctor parameter keeps the 11 existing 4-arg `new PipeRpcWorker(...)` test constructions
valid while DI injects the registered singleton.

## Stage 4 — Architecture / COM confinement
Command: grep -rln "Microsoft.Office.Interop.Outlook|System.Runtime.InteropServices" src/ --include=*.cs
EXIT_CODE: 0
Output Summary: COM remains confined to `src/OpenClaw.MailBridge/ComActiveObject.cs` only. The dispatch
arm and parser carry no COM type; the seam stays plain-data. NetArchTest boundary test re-run green
at P7-T4.

## Stage 5 — Test + Coverage
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --settings mailbridge.runsettings --collect:"XPlat Code Coverage"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 275, Skipped 3 (Total 278). +11 tests vs baseline (6 send_mail
dispatch + 2 OutlookComMailSender guard + 3 provider). MailBridge.Tests cobertura line-rate 93.16%,
branch-rate 86.37% (lines 1390/1492, branches 374/433).

### send_mail dispatch coverage (AC-07, AC-08)
- valid params -> Success, sender received subject/contentType/To/Cc/Bcc/SaveToSentItems=false
- sender throws -> RpcResponse.Failure(InternalError) (D-H)
- no recipients -> InvalidRequest, sender not called (D-G)
- invalid contentType (Markdown) -> InvalidRequest, sender not called
- empty subject -> Success (D-F)
- save-to-sent-items absent -> defaults true and maps to request (AC-08)

## File-size check (all < 500)
- PipeRpcWorker.cs = 438; SendMailRpcHandler.cs = 144; MailBridgeRuntimeTestDoubles.cs = 495;
  SendMailTestDoubles.cs (new) and MailBridgeRuntimeTests.SendMail.cs (new) are well under the cap.
