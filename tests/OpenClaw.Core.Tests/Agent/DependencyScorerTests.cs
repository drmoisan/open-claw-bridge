using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the deterministic dependency scorer (D2, AC-2).
/// </summary>
[TestClass]
public sealed class DependencyScorerTests
{
    private static readonly TriagePolicy Policy = TriagePolicy.FromOptions(
        TestContextBuilder.DefaultPolicyOptions()
    );

    private static readonly string[] SixInternal =
    [
        "a@contoso.com",
        "b@contoso.com",
        "c@contoso.com",
        "d@contoso.com",
        "e@contoso.com",
        "f@contoso.com",
    ];

    [TestMethod]
    public void Score_BaselineInternalMeeting_IsZero()
    {
        var ctx = TestContextBuilder.Context();

        DependencyScorer.Score(ctx, Policy).Should().Be(0);
    }

    [TestMethod]
    public void Score_RecurringAddsTwo()
    {
        var ctx = TestContextBuilder.Context(isRecurring: true);

        DependencyScorer.Score(ctx, Policy).Should().Be(2);
    }

    [TestMethod]
    public void Score_LargeMeetingAddsTwo()
    {
        var ctx = TestContextBuilder.Context(required: SixInternal);

        DependencyScorer.Score(ctx, Policy).Should().Be(2);
    }

    [TestMethod]
    public void Score_ResourceAttendeeAddsOne()
    {
        var ctx = TestContextBuilder.Context(resource: new[] { "room@contoso.com" });

        DependencyScorer.Score(ctx, Policy).Should().Be(1);
    }

    [TestMethod]
    public void Score_OnlineMeetingAddsOne()
    {
        var ctx = TestContextBuilder.Context(isOnlineMeeting: true);

        DependencyScorer.Score(ctx, Policy).Should().Be(1);
    }

    [TestMethod]
    public void Score_ProtectedCategoryAddsThree()
    {
        var ctx = TestContextBuilder.Context(categories: new[] { "Executive" });

        DependencyScorer.Score(ctx, Policy).Should().Be(3);
    }

    [TestMethod]
    public void Score_ProtectedSubjectPatternAddsThree()
    {
        var ctx = TestContextBuilder.Context(subject: "Board review", bodyText: string.Empty);

        DependencyScorer.Score(ctx, Policy).Should().Be(3);
    }

    [TestMethod]
    public void Score_VipOrganizerAddsThree()
    {
        var ctx = TestContextBuilder.Context(organizer: "ceo@contoso.com");

        DependencyScorer.Score(ctx, Policy).Should().Be(3);
    }

    [TestMethod]
    public void Score_ExternalAttendeeAddsTwo()
    {
        var ctx = TestContextBuilder.Context(required: new[] { "client@external.com" });

        DependencyScorer.Score(ctx, Policy).Should().Be(2);
    }

    [TestMethod]
    public void Score_IsAlwaysNonNegative()
    {
        var ctx = TestContextBuilder.Context();

        DependencyScorer.Score(ctx, Policy).Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public void Score_NullContext_Throws()
    {
        var act = () => DependencyScorer.Score(null!, Policy);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Score_NullPolicy_Throws()
    {
        var act = () => DependencyScorer.Score(TestContextBuilder.Context(), null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
