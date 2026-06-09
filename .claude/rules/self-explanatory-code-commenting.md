---
paths:
  - "**/*.py"
description: Intent-first docstring and commenting standards. Applies to Python files.
---

# Code Commenting and Docstring Policy

This rule file summarizes the code commenting and docstring requirements for Python code in this repository.

## Core Principle

Write code that is readable, but assume the maintainer may not know the intent. Docstrings are mandatory for classes and functions. Inline comments explain intent, flow, and decision logic — especially around iteration and branching. Avoid low-value comments that merely narrate the obvious.

## Mandatory Class Docstrings

Every class must have a docstring covering, at minimum:

- **Purpose**: what the class represents or coordinates.
- **Responsibilities**: what it does and does not do (scope boundaries).
- **Usage**: lifecycle, typical call pattern, and collaboration with other objects.
- **High-level flow**: the main steps the class performs or orchestrates.
- **Key invariants/constraints**: expectations that must hold (e.g., sorted inputs, caching semantics).
- **Important side effects**: I/O, persistence, network calls, mutation, concurrency.
- **Attributes** (when non-obvious): what the stored fields mean and how they are populated.

Preferred docstring style: Google-style with `Args:`, `Returns:`, `Raises:`, and `Attributes:` sections where applicable.

## Mandatory Function and Method Docstrings

Every function and method (including private helpers) must have a docstring that includes:

- **Purpose and behavior**: what it accomplishes.
- **Parameters**: meaning, constraints, and how used (types included, even when type hints are present).
- **Returns**: meaning and shape of the return value (or explicitly state `None` for procedures).
- **Raises**: key exceptions that are part of the contract (not every incidental exception, but contract-relevant ones).
- **Side effects**: if it mutates inputs, writes to disk/DB, emits events, etc.

Keep docstrings accurate and contract-oriented. If behavior changes, docstrings must be updated. If a method is a thin wrapper, say so explicitly and explain why the wrapper exists.

## Loops and Comprehensions — Intent Comments Required

Every `for` loop, `while` loop, and non-trivial list/dict/set comprehension must have an **intent comment immediately above it** explaining what the loop accomplishes and why.

- If the intent of a comprehension cannot be explained clearly in one short comment, prefer expanding to an explicit loop with a comment.
- Line-by-line narration of trivial loop counters is not required; explain the purpose of the iteration as a whole.

## Branching — Decision-Logic Comments Required

For any conditional branching beyond a trivial guard clause, add a comment that explains:

- The decision criteria (what distinguishes branches).
- Why the ordering matters (if it does).
- The business/system rationale for why this branching exists.

This applies to `if`/`elif`/`else` chains and `match`/`case` statements. For `match/case`, include a short "routing table" explanation of what each case handle.

## Multi-Step Blocks — Meta-What Comments

When a sequence of tactical lines collectively accomplishes a larger goal, precede the block with a **meta-what + why comment** that describes the overall purpose of the block and the rationale for its approach.

If the block is substantial, strongly prefer extracting it into a helper method with its own docstring.

## 6) Do not number notes

In code comments and docstrings, **do not** use fragile numbered notes like:

* `NOTE 1: ...`
* `NOTE 2: ...`

Prefer comments without tags for general explanations. But if a tag is necessary (e.g. follow-up is needed), use unnumbered tags instead:

* `TODO: ...`
* `WARNING: ...`
* `PERF: ...`
* `SECURITY: ...`

## Meta-What vs. Narration

- **Allowed:** Comments that describe the intent and purpose of a loop, branch, or multi-step block.
- **Prohibited:** Line-by-line narration that merely restates what a single obvious line does.

Example of prohibited narration: `# increment counter` above `count += 1`

Example of allowed meta-what: `# Walk all changed files and collect those that exceed the size limit for the report` above a filtering loop.

## Quality Checklist

Before finalizing code:

- [ ] Docstrings exist for every class and every function/method.
- [ ] Docstrings explain purpose, usage, flow, args, returns, and contract-level raises/side effects.
- [ ] Loops and comprehensions have intent comments (or are expanded for clarity).
- [ ] Branching has decision-logic comments.
- [ ] Multi-step blocks have meta-what + rationale comments above them.
- [ ] No numbered notes (`NOTE 1:`, `NOTE 2:`) appear in comments or docstrings.
- [ ] Comments remain accurate and add real explanatory value.
