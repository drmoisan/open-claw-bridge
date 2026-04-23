# adapt-ci-workflow (Potential Chore / Feature)

- Date captured: 2026-04-23
- Author: drmoisan
- Status: Promoted

## Summary

A copy of `.github/workflows/ci.yml` was added from an unrelated Python/shell repo. The file is not relevant to this repo's .NET + PowerShell + Docker stack, and it is also being silently gitignored by the `.github/` rule in `.gitignore`.

Track the file and adapt the workflow to run .NET build/test and PoshQC checks.

## Environment

- .NET 10 (`global.json` → `10.0.201`).
- PowerShell 7 with PoshQC module at `scripts/powershell/PoshQC/`.
- GitHub Actions with existing `publish.yml` precedent.

## Suspected Cause / Notes

- `.gitignore` excludes `.github/` as a directory. Git cannot re-include descendants of an excluded directory; a `dir/*` + `!dir/file` pattern is required.

## Proposed Fix / Validation Ideas

- [x] Fix `.gitignore` to use `.github/*` + explicit re-includes.
- [x] Rewrite `ci.yml` for .NET + PoshQC + actionlint.
- [x] Validate with `actionlint`.

## Next Step

- [x] Promote to GitHub issue (enhancement template) — Issue #47
- [x] Move to active fix folder / branch — `chore/adapt-ci-workflow-47`
