# Human-Interaction Exception Record — Tenant Validation (issue #119, P4-T2)

Timestamp: 2026-07-06T23-20

This record documents the `human_interaction` requirement that the orchestrator (not this
plan's executor) must record in `artifacts/orchestration/orchestrator-state.json`,
following the F11 HI-1 precedent shape and satisfying the `.claude/rules/orchestrator-state.md`
invariant that an `exception` requirement carries a non-empty `runbook_path`.

## Requirement

- Requirement description: Tenant-side validation of the send-on-behalf workflow cannot be
  automated in this environment or in CI. It requires (a) the Exchange
  `Set-Mailbox -GrantSendOnBehalfTo` grant on each principal mailbox, (b) reconciliation of
  the deployed `OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns` value against the tenant
  grants, (c) one allowed live on-behalf send verified to render as
  "Assistant on behalf of Executive" in Outlook desktop and OWA, and (d) confirmation that
  Send As is absent for the principal/assistant pair. No Azure/Exchange credentials exist in
  this environment or in CI, so these steps are performed by a human administrator per the
  runbook and are not claimed as automated verification.
- response: "exception"
- runbook_path: `docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md`

## Suggested orchestrator-state entry

```json
{
  "human_interaction": {
    "requirements": [
      {
        "description": "Tenant-side send-on-behalf validation (Exchange GrantSendOnBehalfTo grant, allowlist/grant reconciliation, live on-behalf send rendered-appearance check in Outlook and OWA, and Send As absence check) cannot be automated: no Azure/Exchange credentials exist in this environment or CI.",
        "response": "exception",
        "runbook_path": "docs/features/active/2026-07-06-send-on-behalf-allowlist-119/runbooks/send-on-behalf-validation.runbook.md"
      }
    ]
  }
}
```

## Boundary note

Per the plan and `.claude/rules/orchestrator-state.md`, the executor produces this evidence
record only. The orchestrator is responsible for writing the entry into
`artifacts/orchestration/orchestrator-state.json`; this executor did not modify that
checkpoint or its `human_interaction` block.

## Code-side scope (automated, already delivered)

The F15 code-side controls are fully automated and unit-tested against a mocked Graph
client: `SendOnBehalfAuthorizer.Authorize`, the `AllowedPrincipalMailboxUpns` options key
plus its shape-only validator rule and indexed-key binding, and the fail-closed
`GraphHostAdapterClient.SendMailAsync` gate that returns `UNAUTHORIZED` /
`SendOnBehalfDenied` / `Retryable == false` before token acquisition or any HTTP request.
Only tenant-state verification is deferred to the human runbook above.
