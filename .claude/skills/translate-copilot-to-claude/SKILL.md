---
name: translate-copilot-to-claude
description: Translate one or more GitHub Copilot native files (.github/copilot-instructions.md, .github/instructions/*.instructions.md, .github/agents/*.agent.md, .github/prompts/*.prompt.md, .github/skills/<name>/SKILL.md) into the native Claude runtime. Classify each section into agent/rule/skill/hook surfaces, diff against existing .claude/ state, produce a translation plan for user approval, and then apply the plan.
---

# Translate Copilot to Claude

Deterministic translation workflow that ports GitHub Copilot native files into the `.claude/` runtime without duplicating content, overwriting user edits, or breaking existing agents.

## When to Use This Skill

Use this skill when:

- One or more `.github/` native files must be mirrored into `.claude/`.
- An agent persona needs to be split across Claude's four runtime surfaces.
- A batch of instructions files must be re-expressed as path-scoped Claude rules.
- A Copilot prompt or skill must be reshaped into a Claude skill.
- The user wants to know what is missing or out-of-date on the Claude side before making changes.

## Inputs

Required:

- One or more source paths under `.github/`. Accepted types:
  - `.github/copilot-instructions.md`
  - `.github/instructions/<name>.instructions.md`
  - `.github/agents/<name>.agent.md`
  - `.github/prompts/<name>.prompt.md`
  - `.github/skills/<name>/SKILL.md`

Optional:

- `mode=plan-only` — do not write anything; emit the translation plan and stop.
- `mode=apply` — apply the plan after it is produced and approved in the same turn.
- `target-scope=<agent|rule|skill|hook|all>` — restrict surfaces considered for this run.
- `timestamp-override=<ISO-8601>` — override the auto-generated evidence folder timestamp.

Default mode is `plan-only`. Apply requires explicit confirmation either via `mode=apply` or a plain-text approval in the same turn.

## Phase 1 — Intake and Section Extraction

For each input file:

1. Read the full file with `Read`.
2. Parse YAML frontmatter (name, description, tools, applyTo, agent, handoffs, model, other keys).
3. Split the body into top-level sections delimited by `#`, `##`, `###` headings.
4. Classify each section as declarative, procedural, enforceable, identity, or handoff per the taxonomy in Phase 2.
5. Record each section as `{source_path, heading, classification, raw_content}` in an in-memory list.

Never modify or delete source files. Translation is a copy-forward operation.

## Phase 2 — Classification Taxonomy

Apply these decision rules in order. Use the first rule that matches.

### 2.1 Enforceable (hard gate / hard stop) -> `.claude/hooks/`

A section is enforceable when it contains:

- phrases such as "hard gate", "hard stop", "must not", "blocked", "forbidden", "zero-regression", "non-negotiable",
- command or path blacklists that should be rejected regardless of model intent,
- per-batch counting or budget limits,
- toolchain-execution verification ("must run", "no unverified work"),
- test-purity forbiddens (tempfile, network, subprocess in tests).

Target:

- A new or updated script under `.claude/hooks/<name>.ps1`.
- Registration in `.claude/settings.json` under `hooks.PreToolUse` or `hooks.SubagentStop` with a `matcher` that targets the affected tools (`Bash`, `Write|Edit`, specific agent names).

Hooks must be read-only validation gates unless the user explicitly authorizes state mutation. Prefer exit code 0 with a `{"decision":"block","reason":"..."}` JSON response over a non-zero exit when feedback to the agent is required.

### 2.2 Declarative standards (always-on for matching files) -> `.claude/rules/`

A section is declarative when it contains:

- coding conventions (naming, typing, imports, error handling),
- test structure requirements,
- toolchain ordering statements,
- file-size limits,
- language-specific style rules.

Target:

- An existing rule file under `.claude/rules/` that already covers the same path scope, merged into place.
- A new rule file under `.claude/rules/<language-or-topic>.md` with frontmatter:

    ```yaml
    ---
    paths:
      - "<glob>"
    description: "<one line>"
    ---
    ```

- If the source frontmatter used `applyTo: "<glob>"`, convert to `paths: [<glob>]`.

### 2.3 Reusable procedures -> `.claude/skills/<name>/SKILL.md`

A section is procedural when it contains:

- multi-step workflows ("Phase A", "Phase B", "Workflow", numbered steps),
- reporting templates,
- routing decision tables,
- delegation contracts.

Target:

- An existing skill under `.claude/skills/` when one already covers the same concept (check `.claude/skills/<name>/SKILL.md` by name and description).
- A new skill at `.claude/skills/<name>/SKILL.md` when no coverage exists. Use the `make-skill-template` skill as the scaffold if needed. Frontmatter must include `name` and `description`.

Copilot `.github/prompts/*.prompt.md` and `.github/skills/<name>/SKILL.md` default to this target. Preserve the original name when it does not collide with an existing Claude skill.

### 2.4 Identity and preload manifest -> `.claude/agents/<name>.md`

A section belongs on the persona when it contains:

- agent name, description, role summary,
- tool allowlist,
- model selection,
- skill preloads,
- memory scope,
- a short workflow index pointing to preloaded skills,
- stop conditions.

Target:

- An existing agent file under `.claude/agents/<name>.md`. Update only the preload list, tools, description, and stop conditions. Keep the body thin.
- A new agent file when the Copilot persona has no Claude counterpart. Do not duplicate rule, skill, or hook content into the persona body.

### 2.5 Handoffs and delegation -> reference existing skills

Sections describing handoffs to other agents ("delegate to atomic_planner", "handoffs:" frontmatter) should not be duplicated. Reference the canonical skill:

- plan authoring -> `atomic-plan-contract`
- remediation -> `remediation-handoff-atomic-planner`
- promotion lifecycle -> `feature-promotion-lifecycle`
- PR context -> `pr-context-artifacts`
- evidence storage -> `evidence-and-timestamp-conventions`

### 2.6 Repo-wide tone / policy precedence -> `CLAUDE.md`

Sections that state cross-cutting repository defaults (tone, policy reading order, architectural overview) belong in `CLAUDE.md` and should be cross-referenced from `.claude/rules/` rather than restated.

### 2.7 Out-of-scope content

The following content is **not** translated:

- GitHub Actions workflow YAML under `.github/workflows/`.
- Codex or other non-Copilot customizations under `.github/codex/` unless explicitly requested.
- Copilot `chatmodes` unless the user specifies a target Claude surface.

## Phase 3 — Target Resolution

For each classified section, compute a concrete target path:

1. **Hook**: `.claude/hooks/<verb-noun>.ps1` where the verb is `check|enforce|validate` and the noun is the concern (for example, `check-python-test-purity.ps1`, `enforce-python-batch-budget.ps1`).
2. **Rule**: `.claude/rules/<topic>.md` where topic is the language (`python.md`, `csharp.md`, `powershell.md`, `typescript.md`) or cross-cutting concern (`tonality.md`, `general-code-change.md`, `general-unit-test.md`).
3. **Skill**: `.claude/skills/<kebab-case-name>/SKILL.md`. Prefer verb-first names for action skills (`review-feature`, `implement-python`, `invoke-python-engineer`) and topic names for contract skills (`python-qa-gate`, `atomic-plan-contract`).
4. **Agent**: `.claude/agents/<kebab-case-name>.md`. Normalize underscores to hyphens (`python_typed_engineer` -> `python-typed-engineer`).
5. **CLAUDE.md**: single repo-wide file at the workspace root.
6. **settings.json**: `.claude/settings.json` sections `hooks` and `permissions`.

Record target paths in the plan. Do not write anything yet.

## Phase 4 — Existing State Diff

For every target path:

1. `Read` the existing file when it exists.
2. Compute a per-section delta:
   - **add** — the section does not exist in the target.
   - **replace** — the section exists but the source content supersedes it (explicit user intent).
   - **merge** — the section exists and source content must be appended or integrated without removing existing text.
   - **skip** — the section is already present verbatim or has a newer canonical form in the target.
   - **conflict** — the source and target disagree on substantive content; requires user decision.
3. For `.claude/settings.json`, compute add-only updates to `hooks.*` arrays and `permissions.allow`. Never remove existing permissions without explicit instruction.
4. For agent personas already present, prefer **merge** over **replace** to preserve hand-authored guidance.

Flag every `conflict` row in the plan. Conflicts stop Phase 6 for that row until resolved.

## Phase 5 — Translation Plan Artifact

Write a translation plan to `artifacts/translation/<timestamp>/plan.md`. The timestamp is ISO-8601 UTC, for example `2026-04-18T14-30-00Z`, per `evidence-and-timestamp-conventions`.

Plan structure:

```markdown
# Translation Plan: <source file basename(s)>

Generated: <timestamp>
Mode: <plan-only | apply>

## Inputs
- <source path 1>
- <source path 2>

## Mapping Table
| Source Section | Classification | Target Path | Action |
|---|---|---|---|
| <source#section-anchor> | rule | .claude/rules/python.md#pytest-rules | merge |
| <source#section-anchor> | hook | .claude/hooks/check-python-test-purity.ps1 | add |
| <source#section-anchor> | skill | .claude/skills/python-qa-gate/SKILL.md | add |
| <source#section-anchor> | agent | .claude/agents/python-typed-engineer.md | merge |
| <source#section-anchor> | settings | .claude/settings.json | add |

## Conflicts (require user decision)
<one row per conflict, or "none">

## New Files
<list of new file paths>

## Updated Files
<list of modified file paths>

## Settings Delta
- Hooks added: <list>
- Permissions added: <list>
- Matchers extended: <list>

## Evidence Paths
- artifacts/translation/<timestamp>/plan.md
- artifacts/translation/<timestamp>/diff.md (populated after apply)
```

Always produce the plan artifact, even in `mode=apply` runs, for auditability.

## Phase 6 — Apply (only after explicit approval)

Only execute when one of the following holds:

- The user invoked the skill with `mode=apply`.
- The user sent a plain-text approval ("proceed", "apply", "execute the plan") in the same turn after the plan was shown.

Apply order (to minimize breakage):

1. **Rules** — add or merge rule files under `.claude/rules/`.
2. **Skills** — add or merge skill files under `.claude/skills/<name>/SKILL.md`.
3. **Hooks** — add hook scripts under `.claude/hooks/` and parse-check them with `pwsh -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile(..., [ref]$null, [ref]$errors)"`.
4. **Agent personas** — update `.claude/agents/<name>.md` frontmatter (tools, skills, memory) and trim the body to identity + workflow index.
5. **settings.json** — append hook registrations and `Skill(...)` permissions. Never rewrite the file; use targeted edits.
6. **CLAUDE.md** — append cross-references only; never overwrite existing sections.
7. **Evidence** — write `artifacts/translation/<timestamp>/diff.md` summarizing what was actually changed, and store a copy of every new or modified file path under `artifacts/translation/<timestamp>/snapshots/`.

After apply, run a verification sweep:

- Parse-check every new or changed `.ps1` under `.claude/hooks/`.
- JSON-parse `.claude/settings.json` to confirm validity.
- Confirm every new skill appears in the skills catalog by reading back the first five lines of each target file.
- Report the final mapping table with `done` / `skipped` / `conflict-unresolved` statuses.

## Phase 7 — Reporting

Every completion response must include:

1. **Inputs** — list of source files.
2. **Mapping summary** — counts by classification (rule, skill, hook, agent, settings).
3. **Action summary** — counts by action (add, replace, merge, skip, conflict).
4. **Files changed** — explicit list of created and modified paths.
5. **Settings delta** — hooks and permissions added.
6. **Evidence paths** — artifact locations.
7. **Conflicts** — unresolved conflict rows, or "none".

## Guarantees and Prohibitions

- **Idempotency**: running the skill twice on the same input produces no new changes beyond the regenerated plan artifact.
- **No source deletion**: `.github/` files are never modified or removed.
- **No silent overwrites**: existing `.claude/` content is merged by default. Replace requires either user instruction or a `conflict` row resolved explicitly.
- **No hook-based state mutation**: hooks generated by this skill are validation-only unless the user authorizes otherwise.
- **No settings.json rewrites**: only targeted edits that append permissions or matchers.
- **No translation of out-of-scope surfaces**: `.github/workflows/`, `.github/codex/`, and unrecognized file types are skipped and listed under "Skipped inputs" in the plan.

## Classification Quick Reference

| Copilot source pattern | Default Claude target | Notes |
|---|---|---|
| `.github/copilot-instructions.md` | `CLAUDE.md` + `.claude/rules/tonality.md` | Tone and precedence only. |
| `.github/instructions/*.instructions.md` (applyTo) | `.claude/rules/<topic>.md` (paths) | Direct frontmatter rewrite. |
| `.github/agents/*.agent.md` identity frontmatter | `.claude/agents/<name>.md` frontmatter | Convert `tools:` to Claude tool names. |
| `.github/agents/*.agent.md` "Absolute guardrails / hard gate" | `.claude/hooks/*.ps1` + `settings.json` | Hooks enforce; rules merely document. |
| `.github/agents/*.agent.md` "Workflow / Phase X" | `.claude/skills/<workflow>/SKILL.md` | Split per phase when phases are independent. |
| `.github/agents/*.agent.md` "Design rules / Testing rules" | `.claude/rules/<language>.md` | Merge under Design Rules / Pytest Rules subheadings. |
| `.github/agents/*.agent.md` "Handoffs" | Reference existing routing skills | Do not duplicate. |
| `.github/agents/*.agent.md` "Reporting requirements" | `.claude/skills/<name>-qa-gate/SKILL.md` or the QA gate skill | Procedure, not identity. |
| `.github/prompts/*.prompt.md` | `.claude/skills/<action-name>/SKILL.md` | One skill per prompt. |
| `.github/skills/<name>/SKILL.md` | `.claude/skills/<name>/SKILL.md` | Near-1:1; reconcile frontmatter schema. |

## Invocation Examples

- Translate a single agent persona, plan only:
  `translate-copilot-to-claude .github/agents/python-typed-engineer.agent.md`
- Translate a set of instruction files and apply:
  `translate-copilot-to-claude .github/instructions/python-code-change.instructions.md .github/instructions/python-unit-test.instructions.md mode=apply`
- Translate an entire agent bundle (persona + its referenced instructions):
  `translate-copilot-to-claude .github/agents/csharp-typed-engineer.agent.md .github/instructions/csharp-code-change.instructions.md .github/instructions/csharp-unit-test.instructions.md target-scope=all`
