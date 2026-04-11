---
title: "fix-pipe-acl-for-system-admin - Plan"
issue: "TBD"
parent: "none"
owner: "drmoisan"
last_updated: "2026-04-11T10-39"
status: "Draft"
status_color: "lightgrey"
version: "0.1"
---

# fix-pipe-acl-for-system-admin (Potential)

- Date captured: 2026-04-11
- Author: drmoisan
- Status: Draft

## Problem / Why

The private `AddAllowRule` helper in [PipeRpcWorker.cs](src/OpenClaw.MailBridge/PipeRpcWorker.cs) grants `PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance` to every principal it receives. This helper is used for all four allow rules: SYSTEM, Administrators, the primary interactive user, and `openclaw-svc`. The spec requires SYSTEM and Administrators to receive `FullControl`, not the narrower `ReadWrite | CreateNewInstance` combination.

`FullControl` for named pipes is the union of all pipe-specific rights, including `ChangePermissions` and `TakeOwnership`. Without `FullControl`, SYSTEM and Administrators cannot modify the pipe's DACL at runtime if required, and the ACL does not match the stated security requirement (design audit deviation #8, Medium severity). Notably, the NETWORK deny rule already correctly uses `PipeAccessRights.FullControl`, so the inconsistency within the same `BuildPipeSecurity` method is apparent from source inspection.

## Proposed Behavior

`BuildPipeSecurity` must apply different access rights depending on the principal:

- SYSTEM (`WellKnownSidType.LocalSystemSid`) → Allow `PipeAccessRights.FullControl`
- Administrators (`WellKnownSidType.BuiltinAdministratorsSid`) → Allow `PipeAccessRights.FullControl`
- Primary interactive user (current user SID) → Allow `PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance` (unchanged)
- `openclaw-svc` → Allow `PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance` (unchanged)
- NETWORK (`WellKnownSidType.NetworkSid`) → Deny `PipeAccessRights.FullControl` (unchanged)

The simplest implementation adds a separate `AddFullControlRule` helper (or overloads `AddAllowRule` with a rights parameter) and calls it for the SYSTEM and Administrators entries in `BuildPipeSecurity`. The existing `AddAllowRule` that grants `ReadWrite | CreateNewInstance` is retained for the user and service-account entries.

No change to the pipe's overall allow/deny structure, principal list, or `NamedPipeServerStreamAcl.Create` call is required.

## Acceptance Criteria (early draft)

- [ ] After the fix, the `PipeSecurity` returned by `BuildPipeSecurity` grants `PipeAccessRights.FullControl` (Allow) to the SYSTEM SID.
- [ ] After the fix, `BuildPipeSecurity` grants `PipeAccessRights.FullControl` (Allow) to the Administrators built-in SID.
- [ ] `BuildPipeSecurity` continues to grant `PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance` (Allow) to the primary interactive user SID.
- [ ] `BuildPipeSecurity` continues to grant `PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance` (Allow) to the `openclaw-svc` SID.
- [ ] The NETWORK SID deny rule (`PipeAccessRights.FullControl`, Deny) is unchanged.
- [ ] The total number of ACEs in the resulting `PipeSecurity` descriptor remains five (SYSTEM allow, Administrators allow, user allow, `openclaw-svc` allow, NETWORK deny).
- [ ] The bridge builds and all existing unit tests continue to pass after the change.

## Constraints & Risks

- **Narrow, targeted change.** Only the rights granted to SYSTEM and Administrators change. The principal list, the deny rule, and the pipe creation parameters must not be altered.
- **Unit testability constraint.** The existing `Pipe_rpc_worker_build_pipe_security_should_return_descriptor` test in [MailBridgeRuntimeTests.Pipe.cs](tests/OpenClaw.MailBridge.Tests/MailBridgeRuntimeTests.Pipe.cs) currently asserts that `BuildPipeSecurity` does not throw (or throws `IdentityNotMappedException` when `openclaw-svc` is absent). The test does not inspect individual ACE rights. The fix must add assertions that verify the specific rights granted to SYSTEM and Administrators without depending on a real `openclaw-svc` account or interactive session — which means the new assertions should inspect the `PipeSecurity` object's access rules while the test's existing guard for `openclaw-svc` absence is preserved.
- **Windows-only.** `PipeSecurity` and `NamedPipeServerStreamAcl` are Windows-only APIs. The non-Windows guard (`PlatformNotSupportedException`) in `BuildPipeSecurity` must be preserved; no change to the cross-platform guard is needed.
- **`System.IO.Pipes.AccessControl` preview package.** The project currently references version `6.0.0-preview.5.21301.5` (deviation #19). This feature does not require upgrading that package; updating it is a separate remediation item. The `PipeAccessRights.FullControl` member is present in the preview version.
- **No permission escalation risk.** SYSTEM and Administrators already have effective administrative control of the machine. Granting them `FullControl` on the pipe is consistent with standard Windows pipe security practices and does not expand their practical authority over bridge data.

## Test Conditions to Consider

- [ ] **Unit — SYSTEM SID gets FullControl:** After calling `BuildPipeSecurity` (with a stubbed `AccountSidResolver` that returns a valid test SID for `openclaw-svc`), enumerate the access rules on the returned `PipeSecurity` and assert that the rule for `WellKnownSidType.LocalSystemSid` is Allow with `PipeAccessRights.FullControl`.
- [ ] **Unit — Administrators SID gets FullControl:** Same approach; assert the rule for `WellKnownSidType.BuiltinAdministratorsSid` is Allow with `PipeAccessRights.FullControl`.
- [ ] **Unit — user and openclaw-svc retain ReadWrite | CreateNewInstance:** Assert that non-elevated principals are not over-privileged; their rules must not include `FullControl`.
- [ ] **Unit — NETWORK deny unchanged:** Assert that the NETWORK SID rule is Deny with `PipeAccessRights.FullControl`.
- [ ] **Unit — total ACE count is 5:** Assert that `GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))` returns exactly five entries.
- [ ] **Regression — existing build_pipe_security test continues to pass:** No regression in the existing non-Windows guard or `IdentityNotMappedException` detection path.

## Next Step

- [ ] Promote to GitHub issue (feature request template)
- [ ] Create `docs/features/active/fix-pipe-acl-for-system-admin/` folder from the template

