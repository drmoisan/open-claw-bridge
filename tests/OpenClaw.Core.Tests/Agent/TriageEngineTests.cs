using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the deterministic five-way triage engine (D2, AC-2, AC-U2).
/// </summary>
[TestClass]
public sealed class TriageEngineTests
{
    private static readonly TriagePolicy Policy = TriagePolicy.FromOptions(
        TestContextBuilder.DefaultPolicyOptions()
    );

    private static string[] InternalAttendees(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = $"user{i}@contoso.com";
        }

        return result;
    }

    [TestMethod]
    public void Triage_EmptySubjectAndBody_ReturnsIgnore()
    {
        var ctx = TestContextBuilder.Context(subject: string.Empty, bodyText: string.Empty);

        TriageEngine.Triage(ctx, Policy).Decision.Should().Be(TriageDecision.IGNORE);
    }

    [TestMethod]
    public void Triage_PrivateSensitivity_ReturnsPrivateBusyOnly()
    {
        var ctx = TestContextBuilder.Context(sensitivity: "private");

        TriageEngine.Triage(ctx, Policy).Decision.Should().Be(TriageDecision.PRIVATE_BUSY_ONLY);
    }

    [TestMethod]
    public void Triage_VipOrganizer_ReturnsProtectedMeeting()
    {
        var ctx = TestContextBuilder.Context(organizer: "ceo@contoso.com");

        var result = TriageEngine.Triage(ctx, Policy);

        result.Decision.Should().Be(TriageDecision.PROTECTED_MEETING);
        result.Reasons.Should().Contain("Protected organizer");
    }

    [TestMethod]
    public void Triage_ScoreSeven_ReturnsProtectedMeeting()
    {
        // recurring(2) + large(2 via 6 internal) + protected category(3) = 7
        var ctx = TestContextBuilder.Context(
            isRecurring: true,
            required: InternalAttendees(6),
            categories: new[] { "Executive" }
        );

        TriageEngine.Triage(ctx, Policy).Decision.Should().Be(TriageDecision.PROTECTED_MEETING);
    }

    [TestMethod]
    public void Triage_ScoreSix_InternalSender_ReturnsHumanApproval()
    {
        // recurring(2) + large(2) + resource(1) + online(1) = 6 (>=4, <7)
        var ctx = TestContextBuilder.Context(
            isRecurring: true,
            required: InternalAttendees(5),
            resource: new[] { "room@contoso.com" },
            isOnlineMeeting: true
        );

        TriageEngine.Triage(ctx, Policy).Decision.Should().Be(TriageDecision.HUMAN_APPROVAL);
    }

    [TestMethod]
    public void Triage_ScoreFour_InternalSender_ReturnsHumanApproval()
    {
        // recurring(2) + large(2 via 6 internal) = 4
        var ctx = TestContextBuilder.Context(isRecurring: true, required: InternalAttendees(6));

        var result = TriageEngine.Triage(ctx, Policy);

        result.Decision.Should().Be(TriageDecision.HUMAN_APPROVAL);
        result.Reasons.Should().Contain("Moderate dependency score");
    }

    [TestMethod]
    public void Triage_ScoreThree_InternalSender_ReturnsAutoCoordinate()
    {
        // recurring(2) + resource(1) = 3 (<4), internal sender
        var ctx = TestContextBuilder.Context(
            isRecurring: true,
            resource: new[] { "room@contoso.com" }
        );

        TriageEngine.Triage(ctx, Policy).Decision.Should().Be(TriageDecision.AUTO_COORDINATE);
    }

    [TestMethod]
    public void Triage_ExternalSender_ReturnsHumanApproval()
    {
        var ctx = TestContextBuilder.Context(
            messageSender: "stranger@external.com",
            messageFrom: "stranger@external.com"
        );

        var result = TriageEngine.Triage(ctx, Policy);

        result.Decision.Should().Be(TriageDecision.HUMAN_APPROVAL);
        result.Reasons.Should().Contain("External sender or participant present");
    }

    [TestMethod]
    public void Triage_InternalLowDependency_ReturnsAutoCoordinate()
    {
        var ctx = TestContextBuilder.Context();

        TriageEngine.Triage(ctx, Policy).Decision.Should().Be(TriageDecision.AUTO_COORDINATE);
    }

    [TestMethod]
    public void Triage_EmptySenderUsesFrom_ForInternalCheck()
    {
        // Empty sender falls back to the from address, which is external here.
        var ctx = TestContextBuilder.Context(
            messageSender: string.Empty,
            messageFrom: "external@partner.com"
        );

        TriageEngine.Triage(ctx, Policy).Decision.Should().Be(TriageDecision.HUMAN_APPROVAL);
    }

    [TestMethod]
    public void Triage_NullContext_Throws()
    {
        var act = () => TriageEngine.Triage(null!, Policy);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Triage_NullPolicy_Throws()
    {
        var act = () => TriageEngine.Triage(TestContextBuilder.Context(), null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
