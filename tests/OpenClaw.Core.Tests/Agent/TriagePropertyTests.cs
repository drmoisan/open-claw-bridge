using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the deterministic triage layer (D2, AC-2, AC-12).
/// </summary>
[TestClass]
public sealed class TriagePropertyTests
{
    private static readonly TriagePolicy Policy = TriagePolicy.FromOptions(
        TestContextBuilder.DefaultPolicyOptions()
    );

    private static readonly TriageDecision[] AllDecisions = Enum.GetValues<TriageDecision>();

    private static readonly Gen<string> GenEmail = Gen.OneOfConst(
        "user@contoso.com",
        "ceo@contoso.com",
        "client@external.com",
        "room@contoso.com",
        string.Empty
    );

    private static readonly Gen<IReadOnlyList<string>> GenEmails = GenEmail
        .List[0, 8]
        .Select(list => (IReadOnlyList<string>)list.Where(e => e.Length > 0).ToList());

    private static readonly Gen<NormalizedMeetingContext> GenContext = Gen.Select(
            Gen.OneOfConst("Project sync", "Board review", string.Empty, "1:1"),
            Gen.OneOfConst("Body", string.Empty, "agenda"),
            GenEmail,
            GenEmail,
            GenEmails,
            GenEmails,
            Gen.OneOfConst("normal", "private", "confidential"),
            Gen.Bool
        )
        .Select(t =>
        {
            var (subject, body, sender, organizer, required, resource, sensitivity, recurring) = t;
            var all = required.Concat(resource).ToList();
            return new NormalizedMeetingContext(
                MailboxUpn: "owner@contoso.com",
                MessageId: "m",
                ConversationId: "c",
                EventId: "e",
                Subject: subject,
                BodyText: body,
                MessageSender: sender,
                MessageFrom: sender,
                Organizer: organizer,
                RequiredAttendees: required,
                OptionalAttendees: Array.Empty<string>(),
                ResourceAttendees: resource,
                AllAttendees: all,
                Categories: Array.Empty<string>(),
                IsMeetingMessage: true,
                IsOrganizer: false,
                IsRecurring: recurring,
                IsOnlineMeeting: false,
                AllowNewTimeProposals: true,
                Sensitivity: sensitivity,
                ICalUId: null,
                SeriesMasterId: recurring ? "s" : null,
                ReceivedDateTime: null,
                LastModifiedDateTime: null
            );
        });

    [TestMethod]
    public void Score_IsAlwaysNonNegative()
    {
        GenContext.Sample(
            ctx => DependencyScorer.Score(ctx, Policy).Should().BeGreaterThanOrEqualTo(0),
            iter: 1000
        );
    }

    [TestMethod]
    public void Triage_AlwaysReturnsOneOfFiveDecisions()
    {
        GenContext.Sample(
            ctx =>
            {
                var result = TriageEngine.Triage(ctx, Policy);
                AllDecisions.Should().Contain(result.Decision);
            },
            iter: 1000
        );
    }
}
