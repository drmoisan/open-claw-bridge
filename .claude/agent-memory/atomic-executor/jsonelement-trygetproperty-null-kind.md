---
name: jsonelement-trygetproperty-null-kind
description: JsonElement.TryGetProperty throws InvalidOperationException on non-Object kinds, making downstream "?? throw new JsonException" arms unreachable for JSON null entries; verify arm reachability before accepting plan-specified coverage payloads
metadata:
  type: feedback
---

`JsonElement.TryGetProperty` requires an Object-kind element and throws `System.InvalidOperationException` (not `JsonException`) when the element is `JsonValueKind.Null` (or any non-object kind). Any parser shaped like `if (element.TryGetProperty("@removed", ...)) {...} var wire = element.Deserialize<T>() ?? throw new JsonException(...)` therefore has a structurally unreachable `??` arm for a literal `null` array entry — the guard throws first, and the InvalidOperationException escapes executors that catch only `JsonException`/`GraphMappingException`.

**Why:** Issue #117 remediation P3-T3 specified a `"value": [ null ]` payload to cover `GraphDeltaReconciler.ParseDeltaPage`'s `?? throw` arm; the test failed with an escaped InvalidOperationException, proving the plan's assumed behavior did not exist. The nearest reachable payload for the JsonException→TRANSPORT_FAILURE mapping was an entry with a wrong-typed field (`{ "id": 123 }`, number where string expected).

**How to apply:** When a plan or remediation input names a specific payload to cover a branch arm, verify reachability by reading the guard ordering first. A condition-instrumented `??` arm behind a TryGetProperty guard on the same element is dead code (permanent 1/2 in cobertura) — argue the per-file gate with the remaining arms and record the unreachable arm plus the escaped-exception production gap as a follow-up finding rather than forcing an impossible test.
