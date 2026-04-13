# Skipped Tests — Evidence Record

SkippedTests:
  - TestName: Com_active_object_create_and_logon_should_throw_on_non_windows
    Class: MailBridgeRuntimeTests
    Assembly: OpenClaw.MailBridge.Tests
    SkipReason: Runtime OS guard — `if (OperatingSystem.IsWindows()) Assert.Inconclusive("This test targets non-Windows behavior only.")`. The test verifies that `ComActiveObject.CreateAndLogonOutlook()` throws `PlatformNotSupportedException` when executed on a non-Windows OS. It is intentionally skipped on Windows because the Windows-specific COM code path cannot surface the non-Windows exception on that OS. A companion test (`Com_active_object_create_and_logon_should_throw_when_platform_probe_reports_non_windows`) covers the same exception path using a `PlatformProbeComActiveObject` fake, providing equivalent coverage without an OS dependency.
    IssueRisk: Low

  - TestName: PublishOutput_BridgeDirectory_ContainsBridgeExecutable
    Class: MsixPackageTests
    Assembly: OpenClaw.MailBridge.Tests
    SkipReason: Environment variable guard — `if (string.IsNullOrWhiteSpace(MSIX_PUBLISH_DIR)) Assert.Inconclusive("MSIX_PUBLISH_DIR not set – skipping publish-output assertion")`. This test is intended for execution in the MSIX packaging CI pipeline, where the `MSIX_PUBLISH_DIR` environment variable is set to the directory containing published output artifacts. In standard unit-test runs (without a preceding publish step), the artifact directory does not exist and the test is intentionally marked Inconclusive. The reason is documented in both the XML doc comment above the method and the Inconclusive message string.
    IssueRisk: Low

  - TestName: PublishOutput_ClientDirectory_ContainsClientExecutable
    Class: MsixPackageTests
    Assembly: OpenClaw.MailBridge.Tests
    SkipReason: Environment variable guard — same mechanism as `PublishOutput_BridgeDirectory_ContainsBridgeExecutable`. Verifies that `MSIX_PUBLISH_DIR/client/OpenClaw.MailBridge.Client.exe` exists after a publish step. Skipped in all runs where `MSIX_PUBLISH_DIR` is not set. Reason documented in XML doc comment and Inconclusive message.
    IssueRisk: Low
