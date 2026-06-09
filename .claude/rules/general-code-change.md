---
paths:
  - "**"
description: Cross-language code change policy. Applies to all files.
---

# General Code Change Policy

This rule file summarizes the cross-language code change policy for this repository.

## Design Principles

Apply these priorities in order when designing or changing code:

1. **Simplicity first** — Prefer the simplest design that works and is readable. Avoid cleverness and deep indirection.
2. **Reusability** — Factor out logic that is clearly reusable. Avoid copy-paste; share behavior via composition or helper methods.
3. **Extensibility** — Design public APIs so they can be extended without breaking callers. Prefer keyword-style parameters with defaults. Prefer composition over inheritance. Use interfaces/abstract types/protocols to support multiple implementations.
4. **Separation of concerns** — Keep pure logic (transforms, calculations, parsing) separate from I/O (disk, network, DB), UI/CLI, and framework-specific glue.

## Classes, Functions, and APIs

- Create a class when: there is a clear domain concept with data + behavior, state and invariants must travel together, multiple implementations behind an interface are expected, or a multi-step workflow shares context.
- Create a standalone function when: the operation is pure, stateless, and simple; it is a small helper that does not naturally belong on a domain class; or it is a simple transformation from inputs to outputs.
- Keep methods small and focused. Avoid god objects.
- Use interfaces/abstract types/protocols when multiple implementations are likely.

## Module Rigor Tiers

Module rigor tiers (T1–T4) and the uniform-versus-tier-dependent gate matrix are defined in `.claude/rules/quality-tiers.md`. Every project must be classified in `quality-tiers.yml` at repo root.

## Mandatory Toolchain Loop

Run the full seven-stage toolchain in this exact order and repeat until all stages pass in a single pass:

1. **Formatting** (CSharpier for C#; Invoke-Formatter for PowerShell)
2. **Linting** (.NET analyzers for C#; PSScriptAnalyzer for PowerShell)
3. **Type checking** (C# nullable reference analysis; skip for PowerShell)
4. **Architecture-boundary tests** (NetArchTest.Rules or equivalent assertions for C#)
5. **Unit tests** (MSTest for C#; Pester for PowerShell) including property-based tests where applicable per `quality-tiers.md`
6. **Contract / schema compatibility checks** (schema-snapshot diff at host-service boundaries)
7. **Integration tests**

**Restart from step 1** if any stage fails or auto-fixes any files. Do not stop the loop until all seven stages complete without errors in a single pass.

Mutation testing and golden tests run in pre-merge or nightly pipelines, not the per-commit loop.

## File Size Limit

- No production code, test code, or reusable script file may exceed **500 lines**.
- Exceptions: temporary throwaway scripts created and deleted within an agent session; raw text fixtures for language-processing test data; Markdown documentation files.

## Error Handling and Logging

- **Fail fast and explicitly**: raise or return clear, specific errors when invariants are violated.
- Do not silently ignore errors. Do not use broad catch-all handlers unless you immediately re-raise or propagate with added context.
- Use the project's established logging pattern. Log at appropriate levels (`debug`, `info`, `warning`, `error`).
- Enforce invariants at construction/initialization time.
- Use assertions only for internal sanity checks, not user-facing error handling.

## Naming

- Names must be descriptive. Abbreviations are acceptable only when they are standard (`id`, `url`, `db`).
- Language-specific conventions: `snake_case` for Python functions/variables, `PascalCase` for Python classes, `camelCase` for TypeScript/C# locals, `PascalCase` for TypeScript/C# types and public members.

## Public APIs and Compatibility

- Prefer keyword-style parameters with defaults.
- Prefer composition over inheritance when possible.
- Avoid breaking public APIs. If a breaking change is necessary, update all callers in-repo and call it out clearly in the change description.

## Dependencies

- Use only libraries already approved in the project unless explicitly told to add more.
- If adding a dependency is unavoidable, choose a well-maintained, widely used package and document why it is required.

## I/O Boundaries

- Isolate I/O (disk, network, APIs) into specific classes or modules.
- Core domain logic must be testable without touching the network or filesystem.
- Use of temporary files within tests is strictly prohibited.
