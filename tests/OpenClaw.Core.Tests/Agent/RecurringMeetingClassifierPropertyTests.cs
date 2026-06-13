using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the deterministic recurring-meeting classifier (D3, AC-3,
/// AC-12). These complement the example-based <see cref="RecurringMeetingClassifierTests"/>
/// by asserting, over a seeded CsCheck sample, that
/// <see cref="RecurringMeetingClassifier.Classify"/> always returns a defined
/// <see cref="RecurringMeetingKind"/> and that the master Section 10.3 partition
/// invariants hold across the attendee-count boundary. CsCheck prints the failing seed on
/// a <c>Sample</c> failure, satisfying the determinism print-seed requirement.
/// </summary>
[TestClass]
public sealed class RecurringMeetingClassifierPropertyTests
{
    private static readonly RecurringMeetingKind[] AllKinds =
        Enum.GetValues<RecurringMeetingKind>();

    private const string Owner = "owner@contoso.com";
    private const string Organizer = "organizer@contoso.com";

    // Pool of already-normalized (lowercase, no whitespace) attendee emails distinct from
    // the owner and organizer. The source compares raw AllAttendees values against the
    // normalized owner with StringComparison.Ordinal, so generating normalized values
    // keeps the partition invariants deterministic.
    private static readonly Gen<string> GenOtherAttendee = Gen.OneOfConst(
        "alice@contoso.com",
        "bob@contoso.com",
        "carol@contoso.com",
        "dave@contoso.com",
        "erin@contoso.com",
        "frank@contoso.com"
    );

    // ownerEmail is non-null and varies in casing/whitespace so the test also exercises
    // the source's NormalizeEmail call. All variants normalize to Owner.
    private static readonly Gen<string> GenOwnerEmail = Gen.OneOfConst(
        Owner,
        "OWNER@contoso.com",
        "  Owner@Contoso.com  ",
        "owner@CONTOSO.com"
    );

    /// <summary>
    /// Generates a (context, ownerEmail) pair that varies recurrence, the organizer, and
    /// the attendee set. The attendee set sometimes contains only the owner as the single
    /// non-organizer attendee (the ONE_ON_ONE partition) and sometimes spans the
    /// <c>&gt; 5</c> total-attendee boundary (the RECURRING_FORUM partition).
    /// </summary>
    private static readonly Gen<(NormalizedMeetingContext Ctx, string OwnerEmail)> GenCase =
        Gen.Select(
                Gen.Bool, // IsRecurring
                Gen.Int[0, 7], // number of extra non-owner, non-organizer attendees
                Gen.Bool, // include the owner among the attendees
                Gen.Bool, // include the organizer among the attendees
                GenOtherAttendee.List[0, 7],
                GenOwnerEmail
            )
            .Select(t =>
            {
                var (isRecurring, extraCount, includeOwner, includeOrganizer, pool, ownerEmail) = t;

                var attendees = new List<string>();
                if (includeOrganizer)
                {
                    attendees.Add(Organizer);
                }

                if (includeOwner)
                {
                    attendees.Add(Owner);
                }

                // Add up to extraCount distinct pool members (excludes owner/organizer).
                foreach (var email in pool.Distinct().Take(extraCount))
                {
                    attendees.Add(email);
                }

                var ctx = BuildContext(isRecurring, Organizer, attendees);
                return (ctx, ownerEmail);
            });

    [TestMethod]
    public void Classify_AlwaysReturnsDefinedKind()
    {
        GenCase.Sample(
            c =>
            {
                var result = RecurringMeetingClassifier.Classify(c.Ctx, c.OwnerEmail);
                AllKinds.Should().Contain(result);
                Enum.IsDefined(result).Should().BeTrue();
            },
            iter: 1000
        );
    }

    [TestMethod]
    public void Classify_PartitionInvariants_Hold()
    {
        GenCase.Sample(
            c =>
            {
                var ctx = c.Ctx;
                var result = RecurringMeetingClassifier.Classify(ctx, c.OwnerEmail);

                // Mirror the source partition order in RecurringMeetingClassifier.Classify:
                // NON_RECURRING first, then ONE_ON_ONE (only non-organizer attendee is the
                // normalized owner), then RECURRING_FORUM (> 5 total attendees), then
                // RECURRING_OTHER. The owner is normalized via the same rule the source
                // uses (MeetingContextNormalizer.NormalizeEmail).
                if (!ctx.IsRecurring)
                {
                    result.Should().Be(RecurringMeetingKind.NON_RECURRING);
                    return;
                }

                var owner = MeetingContextNormalizer.NormalizeEmail(c.OwnerEmail);
                var others = ctx
                    .AllAttendees.Where(email =>
                        !string.Equals(email, ctx.Organizer, StringComparison.Ordinal)
                    )
                    .ToList();

                if (others.Count == 1 && string.Equals(others[0], owner, StringComparison.Ordinal))
                {
                    result.Should().Be(RecurringMeetingKind.ONE_ON_ONE);
                }
                else if (ctx.AllAttendees.Count > 5)
                {
                    result.Should().Be(RecurringMeetingKind.RECURRING_FORUM);
                }
                else
                {
                    result.Should().Be(RecurringMeetingKind.RECURRING_OTHER);
                }
            },
            iter: 1000
        );
    }

    private static NormalizedMeetingContext BuildContext(
        bool isRecurring,
        string organizer,
        IReadOnlyList<string> attendees
    ) =>
        new(
            MailboxUpn: "owner@contoso.com",
            MessageId: "m",
            ConversationId: "c",
            EventId: "e",
            Subject: "Project sync",
            BodyText: "Body",
            MessageSender: organizer,
            MessageFrom: organizer,
            Organizer: organizer,
            RequiredAttendees: attendees,
            OptionalAttendees: Array.Empty<string>(),
            ResourceAttendees: Array.Empty<string>(),
            AllAttendees: attendees,
            Categories: Array.Empty<string>(),
            IsMeetingMessage: true,
            IsOrganizer: false,
            IsRecurring: isRecurring,
            IsOnlineMeeting: false,
            AllowNewTimeProposals: true,
            Sensitivity: "normal",
            ICalUId: null,
            SeriesMasterId: isRecurring ? "s" : null,
            ReceivedDateTime: null,
            LastModifiedDateTime: null
        );
}
