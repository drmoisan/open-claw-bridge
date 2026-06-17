# env-driven-publish-versioning

- Work Mode: minor-audit
- Tier: T4 (PowerShell dev/build tooling under `scripts/` per `.claude/rules/quality-tiers.md`)

## Problem / Why

The README install instructions hard-code a host-specific absolute repo path and
hard-code the 4-part package version in every `Publish.ps1` invocation. Operators
must edit the version by hand on every release, and the README implies a build may
create a self-signed certificate. The signing thumbprint also has no first-class,
discoverable home; it is only resolvable via a dotnet user secret or a process
environment variable.

Goal: drive the package version and the code-signing certificate thumbprint from
the repository-root `.env` file, auto-increment the version on publish, keep the
self-signed certificate procedure explicit (never run automatically by the build),
and have the cert-creation script persist its thumbprint into `.env`. Update the
README to remove host-specific absolute paths and document both certificate flows.

## Scope (production files — small path, 1-3 production files + tests)

1. `scripts/Publish.Helpers.psm1` — add pure/near-pure helpers for `.env`
   read/update and version increment.
2. `scripts/Publish.ps1` — make `-Version` optional; when absent, read+increment
   `OPENCLAW_PACKAGE_VERSION` from `.env`, persist the new value, and use it.
   Resolve the signing thumbprint from `.env` (`OPENCLAW_CERT_THUMBPRINT`) when
   `-CertThumbprint` is not supplied.
3. `scripts/New-MsixDevCert.ps1` — after creating the self-signed certificate,
   write its thumbprint to `OPENCLAW_CERT_THUMBPRINT` in `.env`.

Supporting (non-production) edits:
- `.env.example` — document `OPENCLAW_PACKAGE_VERSION` and `OPENCLAW_CERT_THUMBPRINT`.
  Preserve the existing pending change on the last line (`OPENCLAW_AGENT_MODEL`).
- `README.md` — remove host-specific absolute paths; document the env-driven
  version and certificate flows (self-sign writes to `.env`; storing an existing
  installed cert thumbprint into `.env`).
- Tests under `tests/scripts/` for the new/changed PowerShell behavior.

## Design Decisions (adopted by orchestrator; reversible defaults)

- D1 `.env` location: repository-root `.env` (gitignored). The repo-root `.env` is
  the same file the Docker/compose stack and onboarding already use.
- D2 New keys: `OPENCLAW_PACKAGE_VERSION` (4-part `^\d+\.\d+\.\d+\.\d+$`) and
  `OPENCLAW_CERT_THUMBPRINT` (40-char hex SHA-1 thumbprint).
- D3 Increment semantics: increment the 4th (revision) segment by 1. This matches
  the existing publish cadence (`1.0.2.0 -> 1.0.2.1`).
- D4 Increment timing: `.env` stores the last-published version. `Publish.ps1`
  (no `-Version`) reads it, increments to the next revision, publishes that, and
  writes the new value back to `.env`.
- D5 `-Version` override: still accepted. When supplied, it is validated, used
  verbatim, and persisted to `OPENCLAW_PACKAGE_VERSION` in `.env`.
- D6 Missing/blank `OPENCLAW_PACKAGE_VERSION` with no `-Version`: fail fast with a
  clear remediation message (do not silently invent a version).
- D7 Cert resolution precedence in `Publish.ps1`: explicit `-CertThumbprint`
  > `.env` `OPENCLAW_CERT_THUMBPRINT` > dotnet user secret `Signing:CertThumbprint`
  > process env `OPENCLAW_CERT_THUMBPRINT`. The existing fail-fast contract is
  preserved: with neither `-SkipSign` nor any resolvable thumbprint, throw before
  any state-changing stage.
- D8 The build never creates a certificate. Certificate creation remains a separate,
  explicit operator step (`New-MsixDevCert.ps1`).
- D9 `.env` writer must preserve unrelated keys, comments, ordering, and append the
  key when absent; update in place when present (idempotent).
- D10 PowerShell design seam: extract `.env` file I/O behind small testable helpers
  (no temp files in tests per repo policy); reuse the wrapper-seam pattern already
  in the module.

## Acceptance Criteria

- AC-1 `scripts/Publish.ps1 -SkipSign` with no `-Version` reads
  `OPENCLAW_PACKAGE_VERSION` from `.env`, publishes the next revision, and writes
  the incremented value back to `.env`.
- AC-2 `scripts/Publish.ps1 -Version 'X.Y.Z.W' -SkipSign` uses `X.Y.Z.W` verbatim
  and persists it to `OPENCLAW_PACKAGE_VERSION` in `.env`.
- AC-3 With no `-Version` and a missing/blank `OPENCLAW_PACKAGE_VERSION`, the
  script throws a clear remediation error before any state-changing stage.
- AC-4 With no `-CertThumbprint` and no `-SkipSign`, the thumbprint is resolved
  from `OPENCLAW_CERT_THUMBPRINT` in `.env` (when present) per the D7 precedence.
- AC-5 `scripts/New-MsixDevCert.ps1` writes the created certificate's thumbprint to
  `OPENCLAW_CERT_THUMBPRINT` in `.env`, preserving other keys/comments.
- AC-6 The `.env` writer is idempotent: re-running updates the value in place and
  does not duplicate the key or disturb unrelated lines.
- AC-7 `.env.example` documents both new keys with guidance comments; the existing
  `OPENCLAW_AGENT_MODEL` line change in the working tree is preserved.
- AC-8 README contains no host-specific absolute repository paths and documents:
  (a) env-driven auto-incrementing publish, (b) the self-sign flow that writes the
  thumbprint to `.env`, and (c) storing an existing installed cert thumbprint into
  `.env`.
- AC-9 PowerShell toolchain passes: PoshQC format -> PSScriptAnalyzer -> Pester,
  with line coverage >= 85% and branch coverage >= 75% on changed code; no new
  analyzer debt; no temp files in tests.

## Dependencies / Risks

- The repo-root `.env` is gitignored; version/cert state is therefore per-clone.
  This is acceptable and intended (local build state).
- `Publish.ps1` and `Publish.Helpers.psm1` are near the 500-line policy cap; keep
  helpers small and within limits.
- Do not weaken the existing strict version `ValidatePattern` or the signing
  fail-fast contract.

## Verification Steps

- Run the PowerShell toolchain (format, analyze, test) via the PoshQC MCP commands.
- Provide Pester coverage evidence (baseline, post-change, comparison) for the
  changed scripts.

## Evidence Checklist
- [x] baseline
- [x] targeted verification
- [x] end-state
