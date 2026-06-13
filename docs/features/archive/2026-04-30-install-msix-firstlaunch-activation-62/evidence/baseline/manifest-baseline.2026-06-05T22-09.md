# Manifest + MsixPackageTests Baseline (P0-T12)

Timestamp: 2026-06-05T22-09

Command:
- `(Get-FileHash -LiteralPath installer/Package.appxmanifest -Algorithm SHA256).Hash`
- `dotnet test OpenClaw.MailBridge.sln --no-build --filter "FullyQualifiedName~MsixPackageTests"`

EXIT_CODE: 0

Output Summary:
- `installer/Package.appxmanifest` SHA-256 (pre-change): `261FD33092D6329453C2A135604554E790D7E3239115FED9566C524A771F6117`
- Identity Version at baseline: `1.0.0.0`.
- MsixPackageTests (pre-change): Passed 6, Skipped 2, Failed 0, Total 8.
  - Active (6): Manifest_ParsesAsValidXml, Manifest_ContainsStartupTaskExtension_WithCorrectExecutable, Manifest_DoesNotDeclareWindowsService, Manifest_IdentityVersion_IsValid4PartVersion, RequiredIconAssets_AllExist, BridgePublishProfile_HasDirectoryLayoutSettings.
  - Skipped (2, env-gated on MSIX_PUBLISH_DIR): PublishOutput_BridgeDirectory_ContainsBridgeExecutable, PublishOutput_ClientDirectory_ContainsClientExecutable.
- Phase 2 adds 3 new tests (protocol extension, no-conflict-with-startupTask, identity >= 1.0.1.0), preserving all existing assertions.
