using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the deterministic recurring-meeting move policy (D3, AC-3).
/// </summary>
[TestClass]
public sealed class MovePolicyTests
{
    private const string Owner = "owner@contoso.com";
    private const string Organizer = "organizer@contoso.com";

    private static readonly OwnerSchedulingPolicy Policy = OwnerSchedulingPolicy.FromOptions(
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

    private static NormalizedMeetingContext Forum() =>
        TestContextBuilder.Context(
            isRecurring: true,
            organizer: Organizer,
            required: InternalAttendees(6)
        );

    [TestMethod]
    public void CanMove_Forum_DeniedForNonOwnerRequester()
    {
        var result = MovePolicy.CanMove(
            Forum(),
            Owner,
            "requester@contoso.com",
            OwnerPriority.P0,
            Policy
        );

        result.Should().BeFalse();
    }

    [TestMethod]
    public void CanMove_Forum_AllowedForOwnerRequester()
    {
        var result = MovePolicy.CanMove(Forum(), Owner, Owner, OwnerPriority.P2, Policy);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void CanMove_Forum_AllowedForMeetingOwnerRequester()
    {
        var result = MovePolicy.CanMove(Forum(), Owner, Organizer, OwnerPriority.P2, Policy);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void CanMove_P0_BumpsSmallNonVipMeeting()
    {
        var meeting = TestContextBuilder.Context(required: new[] { "a@contoso.com" });

        var result = MovePolicy.CanMove(
            meeting,
            Owner,
            "requester@contoso.com",
            OwnerPriority.P0,
            Policy
        );

        result.Should().BeTrue();
    }

    [TestMethod]
    public void CanMove_P0_DeniedWhenVipAttendeePresent()
    {
        var meeting = TestContextBuilder.Context(required: new[] { "ceo@contoso.com" });

        var result = MovePolicy.CanMove(
            meeting,
            Owner,
            "requester@contoso.com",
            OwnerPriority.P0,
            Policy
        );

        result.Should().BeFalse();
    }

    [TestMethod]
    public void CanMove_P0_DeniedWhenSixOrMoreAttendees()
    {
        var meeting = TestContextBuilder.Context(required: InternalAttendees(6));

        var result = MovePolicy.CanMove(
            meeting,
            Owner,
            "requester@contoso.com",
            OwnerPriority.P0,
            Policy
        );

        result.Should().BeFalse();
    }

    [TestMethod]
    public void CanMove_NonForumNonP0_DefaultsToTrue()
    {
        var meeting = TestContextBuilder.Context(required: new[] { "a@contoso.com" });

        var result = MovePolicy.CanMove(
            meeting,
            Owner,
            "requester@contoso.com",
            OwnerPriority.P2,
            Policy
        );

        result.Should().BeTrue();
    }

    [TestMethod]
    public void CanMove_NullMeeting_Throws()
    {
        var act = () => MovePolicy.CanMove(null!, Owner, "r@contoso.com", OwnerPriority.P0, Policy);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CanMove_NullPolicy_Throws()
    {
        var act = () =>
            MovePolicy.CanMove(
                TestContextBuilder.Context(),
                Owner,
                "r@contoso.com",
                OwnerPriority.P0,
                null!
            );

        act.Should().Throw<ArgumentNullException>();
    }
}
