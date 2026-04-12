Timestamp: 2026-04-11T20:44:00-04:00
Command: acceptance-status-summary
EXIT_CODE: 0
Output Summary:
- Sources=docs/features/active/2026-04-10-msix-installer-package-17/spec.md; docs/features/active/2026-04-10-msix-installer-package-17/user-story.md
- SpecDefinitionOfDoneChecked=8
- SpecDefinitionOfDoneUnchecked=3
- SpecSeededTestConditionsChecked=3
- SpecSeededTestConditionsUnchecked=3
- UserStoryAcceptanceCriteriaChecked=8
- UserStoryAcceptanceCriteriaUnchecked=1
- UncheckedSpecItems=scripts/build-msix.ps1 on windows-latest runner; Acceptance criteria 1-9 verified; MsixPackageTests publish-output assertion when MSIX_PUBLISH_DIR is set; upgrade scenario exact v1.1.0.0 wording; uninstall scenario explicit bridge/client directory removal
- UncheckedUserStoryItems=Package can be built from CI using dotnet publish + makeappx.exe (no Visual Studio required)
TotalAcceptanceCriteria: 26
CheckedOffAcceptanceCriteria: 19
RemainingAcceptanceCriteria: 7
RemainingAcceptanceCriteriaItems:
- spec.md :: `scripts/build-msix.ps1` produces a valid `.msix` when invoked after `dotnet publish` on a `windows-latest` runner.
- spec.md :: Acceptance criteria 1–9 verified (see `user-story.md`).
- spec.md :: MSTest `MsixPackageTests.cs`: manifest parses as valid XML; `startupTask` extension present with correct `Executable`; `Version` attribute is a valid 4-part version; `OpenClaw.MailBridge.exe` and `OpenClaw.MailBridge.Client.exe` present in publish output when `MSIX_PUBLISH_DIR` env var is set.
- spec.md :: Upgrade scenario: install v1.0.0.0 → install v1.1.0.0 → startup task still registered → `bridge.settings.json` unchanged.
- spec.md :: Uninstall scenario: `Remove-AppxPackage` → startup task absent from Task Manager → `bridge\` and `client\` directories gone → `bridge.settings.json` still present in `%LOCALAPPDATA%\OpenClaw\MailBridge\`.
- user-story.md :: Package can be built from CI using `dotnet publish` + `makeappx.exe` (no Visual Studio required).
- aggregate :: Not all spec and user-story acceptance checkboxes are complete because CI-run evidence on `windows-latest` is not yet present.
