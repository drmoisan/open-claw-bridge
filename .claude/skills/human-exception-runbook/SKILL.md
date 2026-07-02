# Human-Exception Runbook

Defines the contract for a human-exception runbook: the artifact the orchestrator emits when an unautomatable requirement is resolved with the `exception` response under the autonomous-execution mandate (see `.claude/skills/orchestrate/SKILL.md`).

## When to Use This Skill

Use this skill when:

- The orchestrator detects an unautomatable (human-interaction) requirement and resolves it with the `exception` response rather than `scope_change` or `halt`.
- A permitted exception is recorded in orchestrator state and the schema's exception-requires-runbook invariant must be satisfied (`response == "exception"` requires a non-empty `runbook_path` pointing to an existing file).
- Authoring or reviewing a runbook that a human will follow to complete a step the workflow cannot automate.

## Canonical Path

A human-exception runbook is stored per-feature at:

```
<FEATURE>/runbooks/<name>.runbook.md
```

The `runbook_path` recorded in `orchestrator-state.json` (`human_interaction.requirements[].runbook_path`) is the path relative to the repo root. This path is under the feature folder but is not an `evidence/` sub-path, so it is not governed by `enforce-evidence-locations.ps1` (OD-45-6).

## Required Sections

Every human-exception runbook MUST contain these five sections, in this order:

1. **Cue** — when to act; the event or state that triggers the runbook (for example, "the orchestrator recorded an `exception` for Global-Administrator admin consent").
2. **Prerequisites** — what must be true before the human starts: accounts, roles, devices, tools, and any prior state.
3. **Step-by-step Instructions** — numbered steps, including detailed third-party UI navigation where applicable. Each step is concrete and verifiable.
4. **Verification** — how the human confirms success: the observable state, confirmation dialog, or command output that proves the step completed.
5. **Source and Citation** — the source URL(s) and a dated capture (`updated_at`) for each cited step. Third-party UI sections record the navigation source; non-UI CLI steps record the documentation source for the command used.

## Sourcing Rule (MCP-first / web-second)

Third-party UI steps (for example Azure portal / Entra admin center, Outlook desktop or mobile, the Microsoft 365 admin center) MUST be sourced **MCP-first, web-second**:

1. Prefer an MCP documentation source (for example a Microsoft Learn MCP query) as the primary source.
2. Use a web source (the vendor's current published documentation) only when no MCP source is available.
3. Training data is NOT an acceptable sole source for any third-party UI step, because vendor UIs drift and stale navigation produces incorrect instructions.

Per OD-45-5, the MCP-first / web-second ordering is mandatory for third-party UI navigation. Non-UI CLI steps (for example `az` commands) do not require the UI ordering, but every step type — UI and CLI alike — MUST carry a current, dated citation in the Source-and-Citation section. A runbook step without a dated source is not contract-conformant.

## Conformance

A runbook is contract-conformant when:

- it lives at `<FEATURE>/runbooks/<name>.runbook.md`,
- it contains all five required sections (Cue, Prerequisites, Step-by-step Instructions, Verification, Source and Citation),
- its Source-and-Citation section records at least one source URL and a capture date,
- third-party UI steps were sourced MCP-first / web-second.

A self-contained, conformant example is provided at `.claude/skills/human-exception-runbook/example.runbook.md`.
