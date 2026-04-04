# Dev Container Troubleshooting

## Restore Or Build Fails With A Windows NuGet Fallback Path Error

Symptom:

```text
Unable to find fallback package folder 'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'
```

Cause:

Windows-generated `obj` assets were reused inside Linux.

Fix:

```bash
dotnet restore OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
dotnet build OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
```

If that does not clear the problem, remove stale build outputs and restore again:

```bash
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet restore OpenClaw.MailBridge.sln -p:EnableWindowsTargeting=true
```

## `dotnet run` Fails For The Bridge Or Client

Cause:

Those projects target `net10.0-windows`. The Linux container can build them, but it is not the right runtime for executing the Windows app flow.

Fix:

Run those commands on a Windows host instead of inside the container.

## Outlook COM Checks Fail

Cause:

The container does not include classic Outlook or COM registration.

Fix:

Validate Outlook integration on Windows.

## PowerShell Is Not The Default Terminal

Fix:

Use the terminal profile dropdown and select `pwsh`, or restart the window after the container finishes installing extensions.

## Container Provisioning Fails

Fixes to try:

1. Rebuild the container.
2. Rebuild without cache.
3. Run `bash .devcontainer/verify-container.sh` after the rebuild.

## Codespaces Uses The Wrong Config

Fix:

Recreate the Codespace and let GitHub use `.devcontainer/devcontainer.json`, or explicitly select `.devcontainer/codespaces/devcontainer.json`.
