# Phase 7 Operator Smoke Test Runbook — Issue #62

- Feature: install-msix-firstlaunch-activation (#62)
- Plan: `plan.2026-04-30T00-00.md` (Phase 7 — Operator Smoke Test, AC-10)
- Branch: `bug/install-msix-firstlaunch-activation-62`
- Audience: operator running on a representative Windows host

This runbook covers Phase 7 only. Phases 0-6 are complete and committed. Phase 8
(final QA loop) runs after Phase 7 passes.

## Repository facts pinned for exact commands

- HostAdapter status URL defaults to `http://host.docker.internal:4319/v1`, rewritten
  to `127.0.0.1` on the host, so `http://127.0.0.1:4319/v1/status`. The port is read
  from `OpenClaw__HostAdapter__BaseUrl` in the staged Docker `.env`; if your `.env`
  overrides it, use that port.
- Auth token file: `C:\ProgramData\OpenClaw\HostAdapter\adapter.token`
  (override via `HOSTADAPTER_TOKEN_FILE`).
- Activation URI the installer fires: `openclaw-mailbridge:firstrun`.
- Manifest Identity version is now `1.0.1.0` (independent of the bundle version chosen
  for `Publish.ps1`).

## Goal

Prove on a real Windows host that, after the MSIX install, `Install.ps1` activates
MailBridge via the new `windows.protocol` handler and Stage 8.5 polls `/v1/status`
until MailBridge is ready — that is, the original defect (MailBridge never launched)
is fixed. This closes AC-01 and AC-10.

## Preconditions (all must be true — AC-10)

- Windows 10 (build >= 10.0.17763) or Windows 11.
- Outlook installed and configured for your operator account.
- Docker Desktop installed and running.
- PowerShell 7+ (`pwsh`).
- No prior `OpenClaw.MailBridge` install registered on this host (the criterion
  requires a clean host). Step 1 verifies and, if needed, removes it.
- Your operator Docker `.env` has a non-empty `OPENCLAW_GATEWAY_TOKEN` (the installer's
  env-guard stops otherwise).
- If you do not have a code-signing cert, build the MSIX with `-SkipSign` and install
  with `-AllowUnsigned`; this requires sideloading to be permitted (Settings ->
  For developers -> enable, or an MDM/policy that allows sideloaded apps).

> Run every step in an elevated PowerShell 7 window, from the repo root, on the branch
> `bug/install-msix-firstlaunch-activation-62`.

## Step 0 — Set up the session and evidence folder

```powershell
cd C:\Users\DanMoisan\repos\open-claw-bridge
git switch bug/install-msix-firstlaunch-activation-62
git status   # confirm clean tree, correct branch

# One timestamp reused for every evidence file this run:
$ts  = Get-Date -Format 'yyyy-MM-ddTHH-mm'
$ev  = "docs/features/active/2026-04-30-install-msix-firstlaunch-activation-62/evidence/regression-testing/$ts"
New-Item -ItemType Directory -Force -Path $ev | Out-Null
$ev   # note this path; all artifacts below land here
```

## Step 1 — Confirm a clean host (no prior install, no stale HostAdapter)

```powershell
# Must return nothing. If it returns a package, remove it before continuing.
Get-AppxPackage -Name 'OpenClaw.MailBridge'

# If present:
# Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage

# Confirm no leftover dev HostAdapter is holding the port (default 4319):
Get-NetTCPConnection -LocalPort 4319 -State Listen -ErrorAction SilentlyContinue
# If a stale dev HostAdapter is listening, Install.ps1 -Force will auto-stop it (that
# path is already covered), but for a clean smoke test, stop any 'dotnet run'
# HostAdapter you started manually.
```

## Step 2 — Build a fresh bundle (P7-T1)

Pick the next bundle version (4-part; independent of the manifest's `1.0.1.0`).
Example uses `1.0.1.10`.

```powershell
$version = '1.0.1.10'
.\scripts\Publish.ps1 -Version $version -SkipSign *>&1 | Tee-Object "$ev\publish.log"
# Drop -SkipSign and use -CertThumbprint '<thumb>' instead if you have a signing cert.

$bundle = "artifacts/publish/$version"
Test-Path $bundle    # must be True
```

## Step 3 — Verify the bundled MSIX declares the protocol extension (P7-T2)

```powershell
$msix = Get-ChildItem "$bundle" -Recurse -Filter *.msix | Select-Object -First 1
$tmp  = Join-Path $env:TEMP "msixcheck-$ts"
Copy-Item $msix.FullName "$tmp.zip" -Force
Expand-Archive "$tmp.zip" -DestinationPath $tmp -Force
[xml]$m = Get-Content (Join-Path $tmp 'AppxManifest.xml')

$m.Package.Identity.Version                                   # expect >= 1.0.1.0
$m.Package.Applications.Application.Extensions.Extension |
  Where-Object { $_.Category -eq 'windows.protocol' }         # expect one element, Protocol Name 'openclaw-mailbridge'

# Save the evidence:
@"
Timestamp: $ts
MSIX: $($msix.FullName)
Identity Version: $($m.Package.Identity.Version)
Protocol extension present: $([bool]($m.Package.Applications.Application.Extensions.Extension | Where-Object { $_.Category -eq 'windows.protocol' }))
"@ | Set-Content "$ev\msix-manifest-check.md"
```

Both must hold: Identity version >= `1.0.1.0` and a `windows.protocol` extension naming
`openclaw-mailbridge`. If either is missing, stop — the manifest change did not reach
the bundle (re-plan needed).

## Step 4 — Run the install with a transcript (P7-T3)

Point the two env-file parameters at your operator config (adjust paths to your
machine):

```powershell
$dockerEnv    = "$env:LOCALAPPDATA\OpenClaw\operator-config\.env"
$anthropicEnv = "$env:LOCALAPPDATA\OpenClaw\operator-config\secrets\.env.anthropic"

Start-Transcript -Path "$ev\install-transcript.log" -Force
try {
    & (Join-Path $bundle 'Install.ps1') -Force -AllowUnsigned `
        -DockerEnvFilePath $dockerEnv -AnthropicEnvFilePath $anthropicEnv
}
finally {
    Stop-Transcript
}
```

Pass condition: the transcript reaches `[install:docker] Starting compose stack`
(Stage 9) and `[install:record] Writing install record` (Stage 10) without throwing.
You should also see `[install:msix-activate] Activating MailBridge via protocol handler`
(the new Stage 8b) before Stage 8.5 polling.

## Step 5 — Confirm MailBridge launched in your session (P7-T4)

```powershell
Get-Process OpenClaw.MailBridge -ErrorAction SilentlyContinue |
  Select-Object Id, ProcessName, SessionId, @{n='Path';e={$_.MainModule.FileName}} |
  Tee-Object "$ev\mailbridge-process.txt" | Format-List
```

Pass condition: exactly one `OpenClaw.MailBridge` process, with `SessionId` equal to
your interactive session (not `0`). `SessionId 0` would mean it activated under SYSTEM —
that is a failure and triggers rollback.

## Step 6 — Confirm `/v1/status` reports ready (P7-T5)

```powershell
$token = (Get-Content 'C:\ProgramData\OpenClaw\HostAdapter\adapter.token' -Raw).Trim()
$resp  = Invoke-WebRequest -Uri 'http://127.0.0.1:4319/v1/status' `
           -Headers @{ Authorization = "Bearer $token" } -SkipHttpErrorCheck
$resp.StatusCode                       # expect 200
$resp.Content | Set-Content "$ev\v1-status-body.json"
($resp.Content | ConvertFrom-Json).data.state   # expect a ready state, NOT 'starting' or 'waiting_for_outlook'
```

Pass condition: HTTP `200`, and `data.state` is non-empty and not in
`{starting, waiting_for_outlook}`. (If your `.env` overrode the port, replace `4319`
accordingly.)

## Step 7 — Capture the installed package version (P7-T6)

```powershell
Get-AppxPackage -Name 'OpenClaw.MailBridge' |
  Select-Object PackageFullName, Version |
  Tee-Object "$ev\appx-package.txt" | Format-List
```

Pass condition: a single package, `Version` `1.0.1.0`.

## Step 8 — Write the smoke summary (P7-T7)

```powershell
@"
# Phase 7 Operator Smoke Summary — Issue #62

Timestamp: $ts
Command: Install.ps1 -Force -AllowUnsigned -DockerEnvFilePath <env> -AnthropicEnvFilePath <env-anthropic>
EXIT_CODE: 0

Output Summary:
- Bundle: $bundle (manifest Identity $($m.Package.Identity.Version), protocol extension present)
- Install reached Stage 9 + Stage 10: see install-transcript.log
- MailBridge process (operator session): see mailbridge-process.txt
- /v1/status 200 + ready state: see v1-status-body.json
- Installed package 1.0.1.0: see appx-package.txt

AC-01 and AC-10: CLOSED by the artifacts above.
"@ | Set-Content "$ev\smoke-summary.md"

Get-ChildItem $ev | Select-Object Name, Length
```

## Decision after you run it

- All pass conditions met (Steps 4-7): Phase 7 succeeded. Report the contents of
  `smoke-summary.md`, `v1-status-body.json`, and `mailbridge-process.txt` to the
  orchestrator (or confirm all green and that the artifacts are written). The
  orchestrator then commits the evidence, dispatches `atomic-executor` for Phase 8
  (final QA loop + AC closure), and then `feature-review` for the three audit artifacts
  before the PR into `development`.

- Any pass condition fails (install throws, `SessionId 0`, non-200, state stuck at
  `starting`/`waiting_for_outlook`, or error codes like `0x80073D54` / `0x80070005`):
  stop and do not proceed. Run the plan's Rollback Strategy:

  ```powershell
  Get-AppxPackage -Name 'OpenClaw.MailBridge' | Remove-AppxPackage
  ```

  Then save the failure details (the exact `Install.ps1` invocation, the Stage 8.5
  output from the transcript, the `Get-AppxPackage` and `Get-Process` snapshots, and
  the `/v1/status` body) to `"$ev\smoke-failure.md"` and report them. A failure opens a
  new remediation cycle (the plan notes the likely fallback is
  `Invoke-CommandInDesktopPackage` if protocol activation does not route to the operator
  session).
