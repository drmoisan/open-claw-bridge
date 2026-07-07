# Atomic Planner Memory

- [CSharpier command form](csharpier-command-form.md) — C# format/check plan tasks use `csharpier format .` / `csharpier check .`, not the `dotnet csharpier` driver (no local tool manifest in repo)
- [Raw intermediates path](raw-intermediates-artifacts-csharp.md) — raw TRX/Cobertura/build logs go to `artifacts/csharp/`; only summarizing evidence md goes to `<FEATURE>/evidence/<kind>/`
- [Oversized test files](oversized-test-files-sibling-add.md) — HostAdapterHttpClientTests.cs (616 lines) never modified; plan sibling test ADDs for cap/near-cap test files
