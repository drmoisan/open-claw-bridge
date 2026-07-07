---
name: plan-artifact-crlf-fails-validator
description: The plan validator (validate_orchestration_artifacts, artifact_type plan) fails every phase/task line when the plan file has CRLF line endings; write plan files with LF.
metadata:
  type: project
---

`validate_orchestration_artifacts` with `artifact_type: "plan"` rejects a plan file whose lines end in CRLF (`\r\n`): every `### Phase N — <Title>` heading and every `- [ ] [P#-T#]` task line fails its line-anchored regex because the trailing `\r` is left in the matched line, and the tool reports "Plan does not contain any canonical phase headings."

**Why:** During F20/#124, `atomic-planner` rewrote the plan in place and the working copy picked up CRLF (Windows `core.autocrlf`), so the previously-passing plan suddenly failed validation on ~90 lines. The em-dash (U+2014) and `[x]` checkboxes were NOT the problem — the byte-identical heading passed before the rewrite. Confirmed root cause by diffing: `b"\r\n" in raw` was true for the failing file, false for the passing committed version.

**How to apply:** After any agent Writes/Edits a plan (or other artifact the validator parses line-by-line), normalize to LF before validating: `python -c "p='<path>';raw=open(p,'rb').read().replace(b'\r\n',b'\n');open(p,'wb').write(raw)"`. Then run the validator. Tell delegated planner/executor agents to write artifacts with LF endings. Related: [[checkpoint-validator-contract]].
