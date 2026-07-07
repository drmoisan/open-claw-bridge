# Human-Interaction Deployment Exception Note (P6-T7)

- Timestamp: 2026-07-07T03-05
- Command: `grep -rn "az deployment group create|az deployment sub create" deploy/ .github/ scripts/ tests/`
- EXIT_CODE: 0
- Output Summary: no task in this plan invokes `az deployment group create` or `az deployment sub create`. The only occurrence of either string anywhere in this feature's added files is a documentation comment inside `deploy/azure/parameters/main.dev.bicepparam` (describing how a human operator would later override `containerImage` at deploy time) and the full command text quoted verbatim in `deploy/azure/README.md`'s "Live Deployment (out of scope for automated execution)" section — neither is an executed command.

## Confirmations

- No task in this plan (Phase 0 through Phase 6) executes a live Azure deployment command.
- No runbook file was authored by this plan's tasks. `deploy/azure/README.md` (P3-T2) points to a separately-authored deployment runbook but does not itself contain runbook content, consistent with the plan's Open Questions/Notes: "the runbook itself, are explicitly out of this plan's authoring scope; the runbook is a separate artifact authored by a different agent."
- This requirement is tracked separately in the orchestrator's `human_interaction` block with `response: "exception"`, per `.claude/rules/orchestrator-state.md`'s `human_interaction` invariants, in `artifacts/orchestration/orchestrator-state.json`. This plan's execution does not itself write or modify that file; recording the exception there is the orchestrator's responsibility.
