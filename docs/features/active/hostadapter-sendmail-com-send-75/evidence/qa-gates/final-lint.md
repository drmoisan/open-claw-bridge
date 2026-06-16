# Final QA — Lint / Analyzers

Timestamp: 2026-06-16T09-11
Command: dotnet build OpenClaw.MailBridge.sln -c Debug -p:EnableNETAnalyzers=true -p:EnforceCodeStyleInBuild=true
EXIT_CODE: 0

Output Summary:
Build succeeded. 0 Warning(s), 0 Error(s). No new analyzer suppressions. The only suppression added
by this feature is `[ExcludeFromCodeCoverage]` on the three live-COM-only members of
`OutlookComMailSender` (SendOnSta, AddRecipients, ReleaseRecipients), each annotated as
integration-test-covered; `[ExcludeFromCodeCoverage]` is a coverage attribute, not an analyzer/nullable
suppression. No `#pragma warning disable` or `[SuppressMessage]` was added.
