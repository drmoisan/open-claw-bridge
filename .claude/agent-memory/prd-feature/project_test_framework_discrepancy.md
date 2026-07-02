---
name: test-framework-mstest-not-xunit
description: Tests in this repo use MSTest + FluentAssertions even though .claude/rules/csharp.md says xUnit; specs must cite MSTest.
metadata:
  type: project
---

`tests/OpenClaw.MailBridge.Tests/` uses MSTest (`[TestClass]`/`[TestMethod]`) with FluentAssertions, while `.claude/rules/csharp.md` prescribes xUnit + NSubstitute. Orchestrator delegation prompts also specify MSTest + FluentAssertions (confirmed for issue #19, 2026-07-02). Also confirmed for `tests/OpenClaw.Core.Tests/` (issue #99, 2026-07-02): mocking there is **Moq** (`Mock<T>`, `It.IsAny<>`, `Verify`), not NSubstitute; HTTP is faked with a shared `FakeHttpHandler` and the `HostAdapterHttpClient.TokenReader` init-property seam.

**Why:** The rule file predates or diverges from the actual codebase; following it verbatim would produce specs and test strategies that do not match the project.

**How to apply:** When authoring spec.md test strategies or acceptance criteria for this repo, cite MSTest + FluentAssertions and the existing fake COM test doubles (`MailBridgeRuntimeTestDoubles.cs`, `FakeOutlookItems.LastFilter`), not xUnit. Verify against the actual test tree if in doubt.
