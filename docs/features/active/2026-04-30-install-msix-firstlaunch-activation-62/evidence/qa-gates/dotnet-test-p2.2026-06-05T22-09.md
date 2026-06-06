# MSTest — Phase 2 MsixPackageTests (P2-T7)

Timestamp: 2026-06-05T22-09

Command: `dotnet test OpenClaw.MailBridge.sln --filter "FullyQualifiedName~MsixPackageTests"`

EXIT_CODE: 0

Output Summary:
- Passed: 9, Skipped: 2, Failed: 0, Total: 11.
- Pre-change baseline (P0-T12) had 6 active + 2 skipped. The 3 new tests
  (Manifest_ContainsProtocolExtension_WithOpenClawMailBridgeScheme,
  Manifest_ProtocolExtension_DoesNotConflictWithStartupTask,
  Manifest_IdentityVersion_IsAtLeast_1_0_1_0) all pass; the 6 existing active tests are preserved and still pass; the 2 env-gated publish-output tests remain skipped.
