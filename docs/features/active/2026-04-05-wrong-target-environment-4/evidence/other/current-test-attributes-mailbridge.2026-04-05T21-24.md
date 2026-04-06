# Current Test Attributes — MailBridgeTests.cs

- **Task:** P1-T3
- **Timestamp:** 2026-04-05T21-24
- **Feature:** wrong-target-environment (Issue #4)
- **Source:** `tests/OpenClaw.MailBridge.Tests/MailBridgeTests.cs`

## Namespace Imports (NUnit-specific)

```csharp
using NUnit.Framework;
```

## NUnit Attributes Found

| Location | Attribute | Count |
|---|---|---|
| `MailBridgeTests` class | (none — no `[TestFixture]`) | 0 |
| `Bridge_id_codec_should_follow_spec_prefixes` | `[Test]` | 1 |
| `Settings_validator_rejects_invalid_mode` | `[Test]` | 1 |
| `Body_sanitizer_removes_html_and_paths` | `[Test]` | 1 |

## Required MSTest Migration

- `using NUnit.Framework;` → `using Microsoft.VisualStudio.TestTools.UnitTesting;`
- Add `[TestClass]` to `MailBridgeTests` class.
- `[Test]` (×3) → `[TestMethod]` (×3).
- No `[SetUp]`/`[TearDown]` present — no `[TestInitialize]`/`[TestCleanup]` needed.
- No NUnit-specific assertion APIs — all assertions use FluentAssertions; no changes needed to assertions.
