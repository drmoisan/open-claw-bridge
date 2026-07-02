# Targeted Verification — Changed Surface

Timestamp: 2026-06-16T11-55
Command: Invoke-Pester (New-PesterConfiguration) over the in-scope test files in coverage mode:
  tests/scripts/Publish.Env.Tests.ps1,
  tests/scripts/Publish.Helpers.Tests.ps1,
  tests/scripts/Publish.Helpers.CertThumbprint.Tests.ps1,
  tests/scripts/Publish.Tests.ps1,
  tests/scripts/New-MsixDevCert.Tests.ps1
  CodeCoverage.Path = scripts/Publish.Env.psm1, scripts/Publish.Helpers.psm1, scripts/Publish.ps1, scripts/New-MsixDevCert.ps1
EXIT_CODE: 0

Output Summary:
- Tests: 108 passed, 0 failed across the five in-scope test files.
- Aggregate line/command coverage over the four changed/created scripts: 359/399 = 89.97% (>= 85%).
- Per-file line/command coverage (separate scoped runs):
  - scripts/Publish.Env.psm1 (NEW): 56/57 = 98.25%.
  - scripts/Publish.Helpers.psm1: 185/191 = 96.86%.
  - scripts/Publish.ps1: 91/93 = 97.85%.
  - scripts/New-MsixDevCert.ps1: 27/58 = 46.55%.
- New-MsixDevCert.ps1 coverage is dominated by its `Main` block (the `if ($MyInvocation.InvocationName -ne '.')` guard, lines ~172-209), which never executes under dot-source and was uncovered at baseline as well. The newly added testable function `Save-CertThumbprintToEnv` is fully covered by 4 direct-call tests; the only newly added uncovered lines (the `Save-CertThumbprintToEnv` call site at lines 204-206) are inside that inherently-untestable Main guard, consistent with the pre-existing pattern for every other Main-block line.
- Pester's command-based coverage does not report a separate branch metric; the 89.97% command coverage is the headline (>= 85% line threshold). Branch-relevant paths (precedence branches, update-vs-append, fail-fast guards, -WhatIf paths) are each exercised by dedicated It blocks.
- No temp files created; pure helpers driven with in-memory string[] content; the file-I/O seam is mocked.

AC-to-passing-It mapping:
- AC-1 (no -Version reads/increments/persists): Publish.Env.Tests "increments the revision (4th) segment by one"; Publish.Tests "AC-1: with no -Version reads OPENCLAW_PACKAGE_VERSION, publishes the next revision, and persists it".
- AC-2 (-Version verbatim + persist): Publish.Tests "AC-2: with -Version supplied uses it verbatim and persists it".
- AC-3 (missing/blank version fail-fast before state change): Publish.Tests "AC-3: with no -Version and a missing OPENCLAW_PACKAGE_VERSION throws before any state change" and "...blank...".
- AC-4 (.env cert precedence; call-site .env beats user secret and process env): Publish.Helpers.CertThumbprint.Tests ".env OPENCLAW_CERT_THUMBPRINT beats the dotnet user secret" / "...beats the process-env -EnvThumbprint"; Publish.Tests "AC-4: at the call site, .env OPENCLAW_CERT_THUMBPRINT beats both the dotnet user secret and the process-env value".
- AC-5 (New-MsixDevCert persists thumbprint to .env preserving keys): New-MsixDevCert.Tests "persists OPENCLAW_CERT_THUMBPRINT with the cert thumbprint via Set-EnvFileValue", "writes the updated content via the Write-EnvFileContent seam", "preserves existing keys when persisting".
- AC-6 (.env writer idempotent): Publish.Env.Tests "is idempotent: re-applying the same value does not duplicate the key" (+ update-in-place / append-when-absent It blocks).
- AC-7 (.env.example documents both keys): see Deviations — .env.example edit is BLOCKED by an environment permission restriction (documented in end-state and completion report); the two keys' content is prepared but not written.
- AC-8 (README no host-specific absolute repo paths; three flows documented): verified by README.md edits (grep for host-specific absolute repo paths returns none; env-driven publish, self-sign-writes-.env, and store-existing-cert flows documented).
