Timestamp: 2026-04-07T21-05
Command: manual requirements traceability from repo sources
EXIT_CODE: 0
Output Summary:
- Active feature docs currently require `net8.0-windows`.
- `global.json`, issue #4, and the prior Windows acceptance artifact all identify the repo's authoritative environment as .NET 10.
- Production and test project files also still target `net8.0-windows`, so both docs and code must be corrected before acceptance is rerun.

## Current Feature Docs

- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/issue.md` currently says the completed behavior must retarget all projects to `net8.0-windows`, and its acceptance criteria currently state `All production and test projects target \`net8.0-windows\`; no project remains on \`net10.0-windows\`.`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/spec.md` currently says the completed behavior must retarget all projects to `net8.0-windows`, and its versioning section states `All production and test projects must target \`net8.0-windows\`; the repo remains pinned to SDK \`10.0.201\`, which can build \`net8.0-windows\` projects.`
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/user-story.md` currently lists `All production and test projects target \`net8.0-windows\`; no project remains on \`net10.0-windows\`.` in its acceptance criteria.

## Authoritative .NET 10 Evidence

- `global.json` pins the SDK to `10.0.201` with `rollForward` set to `latestFeature`.
- `docs/features/active/2026-04-05-wrong-target-environment-4/issue.md` documents the already-confirmed regression: the solution incorrectly targeted `net8.0-windows` on a .NET 10 machine, and the proposed fix/acceptance criteria require all MailBridge projects to target `net10.0-windows`.
- `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/windows-acceptance.2026-04-07T20-44.md` shows the most recent publish/install attempt still produced `net8.0-windows` host and client outputs, which is called out as failed operator evidence that must be rerun after restoring the .NET 10 target.

## Required Doc Corrections

- Update `issue.md` so the problem statement, proposed behavior, and acceptance criteria identify `net10.0-windows` as the authoritative branch target.
- Update `spec.md` so the behavior, versioning, and implementation strategy sections require `net10.0-windows` and explicitly describe the current `net8.0-windows` state as a regression from the existing .NET 10 environment.
- Update `user-story.md` so the story and acceptance criteria require `net10.0-windows` and remove the regressed `net8.0-windows` wording.

## Required Code Corrections

- Retarget these project files from `net8.0-windows` to `net10.0-windows`:
  - `src/OpenClaw.MailBridge/OpenClaw.MailBridge.csproj`
  - `src/OpenClaw.MailBridge.Client/OpenClaw.MailBridge.Client.csproj`
  - `src/OpenClaw.MailBridge.Contracts/OpenClaw.MailBridge.Contracts.csproj`
  - `tests/OpenClaw.MailBridge.Tests/OpenClaw.MailBridge.Tests.csproj`
- Update publish/install validation in `scripts/install-mailbridge.ps1` so both runtimeconfig checks require `.NET 10`.
- Update acceptance evidence generation in `scripts/test-mailbridge.ps1` so published/runtime framework keys prove the installed host and client are using the corrected `.NET 10` outputs.

## Acceptance Evidence To Rerun

- Re-run Windows scripted acceptance in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/windows-acceptance.<timestamp>.md` after the refreshed `.NET 10` publish/install.
- Re-run operator validation in `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/windows-operator-validation.<timestamp>.md` against the refreshed install.
- Re-run Phase 7 framework-target verification evidence so installed runtimeconfig files prove `Microsoft.NETCore.App 10.0.0` for both host and client.
