# Benchmark Baseline Provenance

This rule governs performance baselines used by benchmark regression gates. It exists because a baseline captured on a developer workstation was compared against a `windows-latest` runner, producing deterministic latency regressions that the benchmark gate could not survive (issue #26, PR #30).

## Runner-Environment Parity (Required)

Performance baselines must be captured in the same runner environment class against which they are compared. A baseline captured on a developer workstation must not be committed for comparison against a CI runner.

## Prohibited: Unknown Processor

A baseline whose `HostEnvironmentInfo.ProcessorName` is the literal string `"Unknown processor"` is rejected. This value indicates the baseline was captured in an environment where the processor could not be identified (typically a virtualized or developer workstation), which violates runner-environment parity.

- Tooling MUST reject any baseline JSON where `HostEnvironmentInfo.ProcessorName == "Unknown processor"`.
- The rejection is a Blocking finding; the baseline must be recaptured on the target runner class.

## Required: Sibling Provenance File

Every committed baseline file MUST have a sibling `baseline.provenance.json` in the same directory. The provenance file records, at minimum:

- `runner_class` — the runner environment class that produced the baseline (for example `windows-latest`).
- `host_signature` — a stable signature of the host (for example a hashed or labeled description of the CPU/core configuration).
- `workflow_run_url` — the URL of the workflow run that produced the baseline.

- Tooling MUST reject a baseline that has no sibling `baseline.provenance.json`.
- The rejection is a Blocking finding; the baseline must be recaptured with provenance recorded.

## Enforcement

- The validator `scripts/benchmarks/Test-BaselineProvenance.ps1` enforces both rejection conditions above and accepts a runner-captured baseline whose `ProcessorName` is a real processor and whose sibling `baseline.provenance.json` is present.
- The feature-review policy rule `modified-workflow-needs-green-run` (see `.claude/skills/feature-review-workflow/SKILL.md`) provides a second line of defense: a diff under `scripts/benchmarks/**` is Blocking unless a green workflow run against the branch head is present in remediation inputs.

## Scope

- This rule applies to any baseline consumed by a benchmark regression gate.
- It does not change which checks are required by branch protection; it constrains the provenance of the data those checks consume.
