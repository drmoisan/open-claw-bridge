using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Unit tests for the deterministic owner-priority classifier (D3, AC-3).
/// </summary>
[TestClass]
public sealed class OwnerPriorityClassifierTests
{
    private static readonly OwnerSchedulingPolicy Policy = OwnerSchedulingPolicy.FromOptions(
        TestContextBuilder.DefaultPolicyOptions()
    );

    private static NormalizedMeetingContext FromSender(
        string sender,
        string subject = "Meeting request",
        string body = "Please schedule"
    ) => TestContextBuilder.Context(subject: subject, messageSender: sender, messageFrom: sender);

    [TestMethod]
    public void Classify_OwnerInitiated_IsP1()
    {
        var ctx = FromSender("owner@contoso.com");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P1);
    }

    [TestMethod]
    public void Classify_VipSender_IsP0()
    {
        var ctx = FromSender("ceo@contoso.com");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P0);
    }

    [TestMethod]
    public void Classify_UrgentDirectReport_IsP0()
    {
        var ctx = FromSender("report@contoso.com", subject: "Urgent escalation");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P0);
    }

    [TestMethod]
    public void Classify_EmblemDomainSender_IsP1()
    {
        var ctx = FromSender("partner@emblem.email");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P1);
    }

    [TestMethod]
    public void Classify_ExplicitPriority1_IsP1()
    {
        var ctx = FromSender("p1@contoso.com");

        // p1@contoso.com is internal but also on the explicit P1 list, which is checked
        // before the internal-domain P3 rule.
        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P1);
    }

    [TestMethod]
    public void Classify_NonUrgentDirectReport_IsP2()
    {
        var ctx = FromSender("report@contoso.com", subject: "Routine sync");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P2);
    }

    [TestMethod]
    public void Classify_ExplicitPriority2_IsP2()
    {
        var ctx = FromSender("p2@contoso.com");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P2);
    }

    [TestMethod]
    public void Classify_InternalDomainSender_IsP3()
    {
        var ctx = FromSender("colleague@contoso.com");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P3);
    }

    [TestMethod]
    public void Classify_ExplicitPriority3_IsP3()
    {
        var ctx = FromSender("p3@partner.com");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.P3);
    }

    [TestMethod]
    public void Classify_UnknownRecruiter_EscalatesToOwner()
    {
        var ctx = FromSender(
            "stranger@unknown.com",
            subject: "Exciting opportunity",
            body: "I am a recruiter reaching out"
        );

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.ESCALATE_TO_OWNER);
    }

    [TestMethod]
    public void Classify_UnknownExternal_IsDigestIgnored()
    {
        var ctx = FromSender("stranger@unknown.com", subject: "Random promotion", body: "Buy now");

        OwnerPriorityClassifier.Classify(ctx, Policy).Should().Be(OwnerPriority.DIGEST_IGNORED);
    }

    [TestMethod]
    public void Classify_NullContext_Throws()
    {
        var act = () => OwnerPriorityClassifier.Classify(null!, Policy);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Classify_NullPolicy_Throws()
    {
        var act = () => OwnerPriorityClassifier.Classify(TestContextBuilder.Context(), null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
