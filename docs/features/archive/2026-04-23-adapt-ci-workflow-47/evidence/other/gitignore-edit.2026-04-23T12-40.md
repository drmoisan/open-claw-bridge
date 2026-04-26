---
Timestamp: 2026-04-23T12-40
Command: git diff -- .gitignore
EXIT_CODE: 0
---

# .gitignore Edit Diff (P1-T1)

Diff:
```
diff --git a/.gitignore b/.gitignore
index 9a527b0..e849ad2 100644
--- a/.gitignore
+++ b/.gitignore
@@ -73,7 +73,11 @@ secrets/
 *.tmp
 *.temp
 *.cache
-.github/
+.github/*
+!.github/workflows/
+.github/workflows/*
+!.github/workflows/ci.yml
+!.github/workflows/publish.yml
 .tmp-tools/
 .tool-wrappers/
 .claude
```

Output Summary:
- Single hunk at lines 73-79 of `.gitignore` (1 line removed, 5 lines added; net +4 lines, file grew from 79 to 83 lines).
- The five new lines implement the content-based exclusion so `.github/workflows/ci.yml` and `.github/workflows/publish.yml` are trackable while all other `.github/` descendants remain ignored.
- No other lines in `.gitignore` were modified.
