# Phase 4 — Toolchain Gate

Timestamp: 2026-06-16T07-42

Phase 4 adds the `IOutlookMailSender` seam (`SendMailComRequest` plain-data record with the reserved
PI-1 `FromEmailAddress`) and `OutlookComMailSender` (live COM send on the STA). Full-solution stages
(analyzers/nullable across Core, the Core-hosted architecture test) carry the single expected Core
CS0535 break until P7-T2 and are re-verified green at P7-T4 / P11. MailBridge + tests build and pass
independently.

## Stage 1 — Format
Command: csharpier format .
EXIT_CODE: 0
Output Summary: Formatted 184 files. No CSharpier changes beyond new/edited files.

## Stage 2+3 — Lint / Analyzers + Nullable (MailBridge + MailBridge.Tests scope)
Command: dotnet build tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true
EXIT_CODE: 0
Output Summary: Build succeeded. 0 Warning(s), 0 Error(s). (This builds OpenClaw.MailBridge and its
test project, the entire surface Phase 4 touches.)

## Stage 4 — Architecture / COM confinement
Command: grep -rln "Microsoft.Office.Interop.Outlook|System.Runtime.InteropServices" src/ --include=*.cs
EXIT_CODE: 0
Output Summary: COM types remain confined to `src/OpenClaw.MailBridge/ComActiveObject.cs` only. The
new `IOutlookMailSender` seam exposes only plain data (`SendMailComRequest`); no COM type appears in
the seam signature. `OutlookComMailSender` uses late-bound reflection helpers (no Interop import).
NetArchTest boundary test re-run green at P7-T4.

## Stage 5 — Test + Coverage (guard surface)
Command: dotnet test tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj -c Debug --filter "FullyQualifiedName~OutlookComMailSenderGuardTests"
EXIT_CODE: 0
Output Summary: Passed! Failed 0, Passed 2, Skipped 0. The non-COM guard surface of
`SendMailAsync` (null-Application throw; pre-STA cancellation) is covered by unit tests.

## [ExcludeFromCodeCoverage] inventory (live-COM-only)

Exactly three members in `OutlookComMailSender` carry `[ExcludeFromCodeCoverage(Justification=...)]`,
each annotated as integration-test-covered (Phase 9):
- `SendOnSta(object, SendMailComRequest)` — CreateItem -> Subject -> HTMLBody/Body -> recipients ->
  `DeleteAfterSubmit = !SaveToSentItems` (AC-08) -> `Send()` -> release-all in `finally`.
- `AddRecipients(object, IReadOnlyList<string>, int)` — To/CC via `Recipients.Add`, BCC via
  `Recipients.Add(addr).Type = olBCC (3)` (D-I), releasing each transient recipient.
- `ReleaseRecipients(object?)` — releases the Recipients collection in the outer `finally`.

The public `SendMailAsync` orchestration (guard + InvokeAsync wrapper) is NOT excluded and is unit
tested above. No file exceeds 500 lines (`OutlookComMailSender.cs` = 163 lines).
