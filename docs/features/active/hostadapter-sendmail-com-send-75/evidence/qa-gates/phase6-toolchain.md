# Phase 6 — Toolchain Gate

Timestamp: 2026-06-16T08-05

Phase 6 adds the `send-mail` arm to the MailBridge client `Build` switch plus two Build-switch unit
tests. The arm maps the option parser's lower-snaked keys (e.g. `body_content_type`) back to the
hyphenated `send_mail` RPC param keys the bridge handler reads (`body-content-type`), and forwards
recipient JSON arrays verbatim (D-C). Full-solution stages spanning Core remain on the single
expected CS0535 break until P7-T2.

## Stage 1 — Format
Command: csharpier format .
EXIT_CODE: 0
Output Summary: Formatted 187 files. No CSharpier changes beyond new/edited files.

## Stage 2+3 — Lint / Analyzers + Nullable (MailBridge + MailBridge.Tests)
Command: dotnet build tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Warning(s), 0 Error(s). (MailBridge.Client builds via the test
project's reference closure.)

## Stage 4 — Architecture
Command: (no COM/edge change; client depends only on MailBridge.Contracts)
EXIT_CODE: 0
Output Summary: The client arm adds no project reference and no COM type. Rule 3 intact.

## Stage 5 — Test
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~Build_WhenCommandIsSendMail"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 2, Skipped 0. (a) required options -> Method send_mail with
the hyphenated param keys and verbatim recipient JSON; (b) missing --to-recipients -> null.

## File-size check
- src/OpenClaw.MailBridge.Client/Program.cs = 253 lines (< 500).
