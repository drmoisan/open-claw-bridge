---
Timestamp: 2026-04-23T12-40
Command: pwsh -NoProfile -Command "(Get-FileHash -LiteralPath .github/workflows/publish.yml -Algorithm SHA256).Hash"
EXIT_CODE: 0
---

# publish-yml-unchanged — invariant evidence

Post-rewrite SHA-256:
```
049D259384E5FB3806B000DFB31907E41C58A7EEA45874A95282EF6111FECCFD
```

Baseline SHA-256 (from `evidence/baseline/publish-yml-hash.2026-04-23T12-40.md`):
```
049D259384E5FB3806B000DFB31907E41C58A7EEA45874A95282EF6111FECCFD
```

Output Summary:
- MATCHES_BASELINE: true
- `.github/workflows/publish.yml` is byte-identical to its Phase 0 state after all Phase 2 edits.
- Issue Out of Scope item "do not modify `publish.yml`" is preserved.
