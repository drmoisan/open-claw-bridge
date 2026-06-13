using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the deterministic recurring-meeting classifier (D3, AC-3).
/// </summary>
[TestClass]
public sealed class RecurringMeetingClassifierTests
{
    private const string Owner = "owner@contoso.com";
    private const string Organizer = "organizer@contoso.com";

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
    public void Classify_NonRecurring_IsNonRecurring()
    {
        var ctx = TestContextBuilder.Context(isRecurring: false);

        RecurringMeetingClassifier
            .Classify(ctx, Owner)
            .Should()
            .Be(RecurringMeetingKind.NON_RECURRING);
    }

    [TestMethod]
    public void Classify_OrganizerPlusOwnerOnly_IsOneOnOne()
    {
        var ctx = TestContextBuilder.Context(
            isRecurring: true,
            organizer: Organizer,
            required: new[] { Organizer, Owner }
        );

        RecurringMeetingClassifier
            .Classify(ctx, Owner)
            .Should()
            .Be(RecurringMeetingKind.ONE_ON_ONE);
    }

    [TestMethod]
    public void Classify_MoreThanFiveAttendees_IsRecurringForum()
    {
        var ctx = TestContextBuilder.Context(
            isRecurring: true,
            organizer: Organizer,
            required: InternalAttendees(6)
        );

        RecurringMeetingClassifier
            .Classify(ctx, Owner)
            .Should()
            .Be(RecurringMeetingKind.RECURRING_FORUM);
    }

    [TestMethod]
    public void Classify_OtherRecurring_IsRecurringOther()
    {
        var ctx = TestContextBuilder.Context(
            isRecurring: true,
            organizer: Organizer,
            required: new[] { Organizer, Owner, "third@contoso.com" }
        );

        RecurringMeetingClassifier
            .Classify(ctx, Owner)
            .Should()
            .Be(RecurringMeetingKind.RECURRING_OTHER);
    }

    [TestMethod]
    public void Classify_NullContext_Throws()
    {
        var act = () => RecurringMeetingClassifier.Classify(null!, Owner);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Classify_NullOwner_Throws()
    {
        var act = () => RecurringMeetingClassifier.Classify(TestContextBuilder.Context(), null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
