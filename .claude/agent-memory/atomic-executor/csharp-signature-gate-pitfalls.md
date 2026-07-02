---
name: csharp-signature-gate-pitfalls
description: Two C# mechanics that break plan build-gate ordering — optional-parameter insertion breaks positional call sites, and CS9113 fires on primary-constructor params unread until a later partial exists
metadata:
  type: feedback
---

Two recurring C# pitfalls that invalidate plan/preflight "compiles unchanged" claims in this repo (hit during issue #107 execution):

1. Inserting an optional parameter before `CancellationToken ct` requires `ct = default` too (optional params must trail required ones), and any existing POSITIONAL call `M(x, token)` then fails (CancellationToken cannot bind to `string?`). Only named-argument calls (`cancellationToken: ct`) survive. Repo pattern to mirror: `IHostAdapterClient.SendMailAsync(request, string? requestId = null, CancellationToken cancellationToken = default)`.

2. CS9113 ("parameter is unread") fires on a primary-constructor parameter until some partial reads it — a plan that gates `dotnet build` (zero warnings) between adding the constructor param and creating the partial that uses it cannot pass the gate as ordered.

**Why:** Preflight for #107 claimed "Pipeline call site compiles unchanged with the null default"; both issues surfaced mid-execution and required minimal in-task micro-fixes (named `ct:` argument; running the next task's partial creation before recording the gate).

**How to apply:** During preflight of any plan that adds a parameter to an existing C# method or primary constructor, check for positional call sites and for gate tasks ordered before the parameter's first use; flag as plan delta instead of discovering at the gate.
