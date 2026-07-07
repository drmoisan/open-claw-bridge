Timestamp: 2026-07-07T02-30

## Human-Interaction Checkpoint (AC5)

Live-tenant Purview/Graph activity-log ingestion verification — confirming that a
real Microsoft Purview/Graph activity log ingests the `PurviewActivityLogRecord`
shape projected by `PurviewActivityLogProjection.Project` for an actual CloudSync
activity event — cannot be performed in this environment or in CI: no
Azure/Exchange/Purview tenant credentials exist here (spec.md Constraints & Risks;
research §3).

This requires a `human_interaction` entry in `artifacts/orchestration/orchestrator-state.json`
with:

- `response: "exception"`
- a non-empty `runbook_path`

Recommended `runbook_path` (per this plan):
`docs/features/active/2026-07-07-graph-activity-log-purview-124/runbooks/purview-live-tenant-ingestion.runbook.md`

This mirrors the F11 HI-1 (`docs/features/active/2026-07-02-exchange-rbac-scripts-111/runbooks/exchange-rbac-setup.runbook.md`)
and F17 (`docs/features/active/2026-07-06-negative-scope-smoke-test-120/runbooks/negative-scope-startup-validation.runbook.md`)
precedent for human-exception runbooks recorded against `human_interaction`
requirements this repository/CI environment cannot execute.

**Runbook authorship is explicitly out of this plan's scope.** This plan records
the checkpoint only; the orchestrator must delegate authoring the runbook content
to the `human-exception-runbook` agent separately, after this plan's preflight
clears.

### Factual Observation (not authored by this plan)

At the time this checkpoint evidence was written, a runbook already exists on disk
at `docs/features/active/2026-07-07-graph-activity-log-purview-124/runbooks/purview-activity-log-live-tenant-verification.runbook.md`
(filename differs slightly from this plan's recommended path above). This artifact
was not created by this executor or by any task in this plan; it appears to be the
product of a separately-dispatched `human-exception-runbook` agent run. This
executor did not author, modify, or verify that runbook's content and makes no
claim about its completeness or correctness — it is noted here only as an
observed fact for the orchestrator's reconciliation of the `human_interaction`
checkpoint.
