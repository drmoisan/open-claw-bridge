# CI Workflow Authoring

This rule governs GitHub Actions workflow steps that run PowerShell (`pwsh`). It exists because a `pwsh` step that intentionally invoked a failing nested command left `$LASTEXITCODE == 1` after its verification logic had already succeeded, leaking a failure to GitHub Actions even though the step's intent was satisfied (issue #26, PR #30).

## Deliberately-Failing Nested Command Pattern (Required)

A workflow step whose `run:` block intentionally invokes a command expected to fail — for example a negative-path self-validation that asserts a gate catches a synthetic regression — MUST not allow the residual non-zero exit code to propagate to GitHub Actions.

For any such step, the `run:` block MUST do one of the following:

1. Reset the exit code explicitly after the expected failure:
   ```powershell
   & ./some-tool --expect-failure
   $LASTEXITCODE = 0
   ```
2. Or terminate the success path with an explicit zero exit:
   ```powershell
   if ($verificationSucceeded) { exit 0 } else { exit 1 }
   ```

A `pwsh` step terminates with the exit code of the last external command unless the script explicitly resets it or calls `exit`. Negative-path verification steps therefore require an explicit reset or explicit `exit 0`.

## Rationale

- GitHub Actions interprets a step's process exit code as the step result. A leaked `$LASTEXITCODE` from an intentionally-failing nested command causes a passing verification to report failure.
- No local toolchain stage executes a workflow's `run:` block, so this defect is invisible to local feature-review. This textual rule is the artifact local review cites when reading workflow YAML.

## Enforcement

- Local feature-review cites this rule when reviewing diffs that add or modify `pwsh` steps with deliberately-failing nested commands.
- The feature-review policy rule `modified-workflow-needs-green-run` (see `.claude/skills/feature-review-workflow/SKILL.md`) requires a green workflow run against the branch head before a workflow change can merge, which exercises the exit-code path on the runner.

## Scope

- This rule applies to any workflow step whose `run:` block uses `shell: pwsh` (or the repo default `pwsh`) and intentionally invokes a failing nested command.
- It does not change required-check configuration or branch protection.
