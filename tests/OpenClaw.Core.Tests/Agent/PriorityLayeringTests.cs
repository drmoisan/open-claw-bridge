using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Tests for the deterministic layering rule (AC-4, AC-U2): the priority and
/// move-policy layer is exercised only for <c>AUTO_COORDINATE</c>/<c>HUMAN_APPROVAL</c>
/// triage results; <c>PROTECTED_MEETING</c> and <c>PRIVATE_BUSY_ONLY</c> are never
/// passed into the priority layer.
/// </summary>
[TestClass]
public sealed class PriorityLayeringTests
{
    [DataTestMethod]
    [DataRow(TriageDecision.AUTO_COORDINATE, true)]
    [DataRow(TriageDecision.HUMAN_APPROVAL, true)]
    [DataRow(TriageDecision.PROTECTED_MEETING, false)]
    [DataRow(TriageDecision.PRIVATE_BUSY_ONLY, false)]
    [DataRow(TriageDecision.IGNORE, false)]
    public void RequiresPriorityLayer_GatesOnDecision(TriageDecision decision, bool expected)
    {
        SchedulingGate.RequiresPriorityLayer(decision).Should().Be(expected);
    }

    [TestMethod]
    public void ProtectedMeeting_IsNotPassedIntoPriorityLayer()
    {
        // A protected meeting remains protected regardless of requester priority: the
        // gate denies entry to the priority layer.
        SchedulingGate.RequiresPriorityLayer(TriageDecision.PROTECTED_MEETING).Should().BeFalse();
    }

    [TestMethod]
    public void PrivateBusyOnly_IsNotPassedIntoPriorityLayer()
    {
        // A private item remains opaque regardless of priority: the gate denies entry.
        SchedulingGate.RequiresPriorityLayer(TriageDecision.PRIVATE_BUSY_ONLY).Should().BeFalse();
    }
}
