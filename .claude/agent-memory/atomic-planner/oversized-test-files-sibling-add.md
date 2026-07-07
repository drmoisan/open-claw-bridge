---
name: oversized-test-files-sibling-add
description: HostAdapterHttpClientTests.cs (616 lines) and near-cap HostAdapterSchedulingServiceTests.cs must not be extended; plan sibling test ADDs instead
metadata:
  type: feedback
---

When a delegation prompt or research lists `tests/OpenClaw.Core.Tests/HostAdapterHttpClientTests.cs` as a test MODIFY, convert it to a test ADD in a new sibling file (e.g., `HostAdapterHttpClient<Feature>Tests.cs` referencing the shared `FakeHttpHandler` defined in that file).

**Why:** The file is a pre-existing 500-line-cap violation (616 lines, verified 2026-07-07). F18 (#128) preflight revision 1 established this ruling and the sibling-file pattern (`HostAdapterHttpClientRescheduleTests.cs`); F19 (#130) delegation repeated the same modify instruction and was corrected the same way. `HostAdapterSchedulingServiceTests.cs` is near-cap (480 lines) — new service-seam cases also go to siblings (F18 executed split: `HostAdapterSchedulingServiceRescheduleTests.cs`).

**How to apply:** In any open-claw-bridge C# plan, verify line counts of test files listed as MODIFY targets before writing tasks; files at or near the 500-line cap get sibling ADD tasks and an explicit "remains unmodified" constraint plus an Open Questions note recording the deviation from the delegation prompt. Related: [[csharpier-command-form]].
