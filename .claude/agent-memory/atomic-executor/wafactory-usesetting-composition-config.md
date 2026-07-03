---
name: wafactory-usesetting-composition-config
description: WebApplicationFactory config for values read at composition time in Program.cs must go through UseSetting, not ConfigureAppConfiguration
metadata:
  type: feedback
---

When a test drives `Program.cs` code that reads `builder.Configuration` directly at composition time (e.g., a backend-selection flag checked before `builder.Build()`), supply the value via `IWebHostBuilder.UseSetting(key, value)` inside `WithWebHostBuilder`, NOT via `ConfigureAppConfiguration`.

**Why:** Under minimal hosting, `WebApplicationFactory`'s `ConfigureAppConfiguration` callbacks are applied after the app's own builder code has already executed, so direct `builder.Configuration.GetValue(...)` reads see nothing (verified in issue #115: the opt-in selection test kept resolving the default client until switched to `UseSetting`). Options-bound values still work either way because binding is deferred to resolve time.

**How to apply:** In OpenClaw `GraphBackendSelectionTests`-style composition-root tests, use `builder.UseSetting("OpenClaw:GraphAdapter:Enabled", "true")` etc. Related: Moq cannot proxy `ILogger<T>` closed over an internal type (DynamicProxy needs InternalsVisibleTo) — use a hand-rolled capturing logger in the test file instead.
