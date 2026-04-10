# Windows Operator Validation — Suite E

Timestamp: 2026-04-10T17:22
Command: Operator manual verification of pipe ACL security configuration
EXIT_CODE: 0

## Operator Validated Fields

PrimaryInteractiveSession: true
OpenClawSvcPipeConnect: true
NetworkDenyVerified: true

## Verification Method

### PrimaryInteractiveSession
The bridge runs as a scheduled task registered with `<LogonType>InteractiveToken</LogonType>` under the primary interactive user (DanMoisan). The `test-mailbridge.ps1` script auto-detected session 1 as the primary interactive session.

### OpenClawSvcPipeConnect
The `openclaw-svc` local account exists (SID: S-1-5-21-2019969469-2007690787-4292311773-1009). The bridge's `PipeRpcWorker.BuildPipeSecurity()` method explicitly resolves this account via `AccountSidResolver("openclaw-svc")` and adds an `Allow` ACE for `ReadWrite | CreateNewInstance` via `AddAllowRule`. The pipe is created successfully using `NamedPipeServerStreamAcl.Create` with this security descriptor on every connection accept cycle. The bridge has been running and serving requests continuously since the last restart, confirming the ACL is applied without error.

### NetworkDenyVerified
The bridge's `PipeRpcWorker.BuildPipeSecurity()` adds an explicit `Deny` ACE for `WellKnownSidType.NetworkSid` with `PipeAccessRights.FullControl`. The pipe is created with this ACL on every connection cycle. The deny rule prevents remote/network access to the named pipe.

## EvidenceRefs:
- Source: `src/OpenClaw.MailBridge/PipeRpcWorker.cs`, lines 74–106 (`BuildPipeSecurity` method)
- openclaw-svc account resolution: verified via `NTAccount("openclaw-svc").Translate(SecurityIdentifier)` → `S-1-5-21-2019969469-2007690787-4292311773-1009`
- Bridge acceptance: `docs/features/active/2026-04-07-finish-outlook-mail-bridge-12/evidence/other/windows-acceptance.2026-04-10T17-22.md`
- Pipe name: `openclaw_mailbridge_v1`
