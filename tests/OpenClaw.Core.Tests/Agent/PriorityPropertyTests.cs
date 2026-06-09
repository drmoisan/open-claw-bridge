using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the deterministic priority and move-policy layer (D3,
/// AC-3, AC-12).
/// </summary>
[TestClass]
public sealed class PriorityPropertyTests
{
    private static readonly OwnerSchedulingPolicy Policy = OwnerSchedulingPolicy.FromOptions(
        TestContextBuilder.DefaultPolicyOptions()
    );

    private static readonly OwnerPriority[] AllPriorities = Enum.GetValues<OwnerPriority>();

    private static readonly Gen<string> GenSender = Gen.OneOfConst(
        "owner@contoso.com",
        "ceo@contoso.com",
        "report@contoso.com",
        "p1@contoso.com",
        "p2@contoso.com",
        "p3@partner.com",
        "colleague@contoso.com",
        "partner@emblem.email",
        "stranger@unknown.com"
    );

    private static readonly Gen<NormalizedMeetingContext> GenContext = Gen.Select(
            GenSender,
            Gen.OneOfConst("Sync", "Urgent review", "Opportunity", string.Empty),
            Gen.Bool,
            Gen.Int[0, 8]
        )
        .Select(t =>
        {
            var (sender, subject, recurring, attendeeCount) = t;
            var attendees = Enumerable
                .Range(0, attendeeCount)
                .Select(i => $"user{i}@contoso.com")
                .ToList();
            return TestContextBuilder.Context(
                subject: subject,
                messageSender: sender,
                messageFrom: sender,
                organizer: "organizer@contoso.com",
                required: attendees,
                isRecurring: recurring
            );
        });

    [TestMethod]
    public void Classify_AlwaysReturnsDefinedPriority()
    {
        GenContext.Sample(
            ctx =>
            {
                var priority = OwnerPriorityClassifier.Classify(ctx, Policy);
                AllPriorities.Should().Contain(priority);
            },
            iter: 1000
        );
    }

    [TestMethod]
    public void CanMove_Forum_DeniedForNonOwnerNonMeetingOwnerRequester()
    {
        // Build forums (recurring with > 5 attendees) and a requester who is neither the
        // owner nor the organizer; CanMove must be false.
        var genForum = Gen.Select(
                Gen.Int[6, 12],
                Gen.OneOfConst(OwnerPriority.P0, OwnerPriority.P2)
            )
            .Select(t =>
            {
                var (count, priority) = t;
                var attendees = Enumerable
                    .Range(0, count)
                    .Select(i => $"user{i}@contoso.com")
                    .ToList();
                var meeting = TestContextBuilder.Context(
                    isRecurring: true,
                    organizer: "organizer@contoso.com",
                    required: attendees
                );
                return (meeting, priority);
            });

        genForum.Sample(
            t =>
            {
                var (meeting, priority) = t;
                MovePolicy
                    .CanMove(meeting, "owner@contoso.com", "outsider@contoso.com", priority, Policy)
                    .Should()
                    .BeFalse();
            },
            iter: 1000
        );
    }
}
