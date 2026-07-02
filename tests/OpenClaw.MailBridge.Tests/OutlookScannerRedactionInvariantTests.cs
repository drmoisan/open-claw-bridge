using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Deterministic invariant tests for the three pure redaction functions of issue #18
/// (remediation cycle 1, Fix 2 option (b) — see
/// <c>evidence/other/property-test-decision.2026-07-02T10-07.md</c>): full-domain equivalence for
/// <c>IsSensitive</c>, and exact-protected-set transformation, mechanical-field preservation, and
/// idempotence matrices for <c>RedactMessage</c> and <c>RedactEvent</c>. No randomness; every
/// input is enumerated explicitly.
/// </summary>
[TestClass]
public sealed class OutlookScannerRedactionInvariantTests
{
    private static readonly DateTimeOffset FixedReceived = new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedStart = new(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);

    // Domain-sampling rationale: the Outlook olSensitivity enum defines exactly 0..3, so that
    // range is enumerated exhaustively; outside the defined range the domain is unbounded int?,
    // so it is boundary-sampled (int.MinValue, -1 below; 4, 5, 99, int.MaxValue above) plus null.
    private static readonly int?[] SensitivityDomain =
    [
        0,
        1,
        2,
        3,
        null,
        int.MinValue,
        -1,
        4,
        5,
        99,
        int.MaxValue,
    ];

    [TestMethod]
    public void IsSensitive_should_match_two_or_three_over_the_full_domain()
    {
        SensitivityDomain.Should().HaveCount(11);

        foreach (var sensitivity in SensitivityDomain)
        {
            OutlookScanner
                .IsSensitive(sensitivity)
                .Should()
                .Be(
                    sensitivity is 2 or 3,
                    "IsSensitive({0}) must be true only for 2 and 3",
                    sensitivity?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        ?? "null"
                );
        }
    }

    private static MessageDto[] MessageMatrix() =>
        [
            // Fully populated protected fields, mail kind, Sensitivity 2.
            new MessageDto(
                BridgeId: "olk:msg:mail-populated",
                ItemKind: "mail",
                Subject: "Secret subject",
                ReceivedUtc: FixedReceived,
                SentUtc: FixedReceived.AddMinutes(-4),
                Importance: 2,
                Sensitivity: 2,
                Unread: true,
                HasAttachments: true,
                MessageClass: "IPM.Note",
                SenderName: "Secret Sender",
                SenderEmail: "secret@example.com",
                ToJson: """["to@example.com"]""",
                CcJson: """["cc@example.com"]""",
                BodyPreview: "Secret body",
                ProtectedFieldsAvailable: true,
                IsRedacted: false,
                SenderEmailResolved: "secret.resolved@example.com",
                FromEmailAddress: "from@example.com",
                ConversationId: "conv-1",
                MeetingMessageType: null
            ),
            // Fully populated protected fields, meeting kind, Sensitivity 3.
            new MessageDto(
                BridgeId: "olk:msg:meeting-populated",
                ItemKind: "meeting",
                Subject: "Secret meeting request",
                ReceivedUtc: FixedReceived.AddHours(1),
                SentUtc: FixedReceived.AddHours(1).AddMinutes(-9),
                Importance: 1,
                Sensitivity: 3,
                Unread: false,
                HasAttachments: false,
                MessageClass: "IPM.Schedule.Meeting.Request",
                SenderName: "Secret Organizer",
                SenderEmail: "organizer@example.com",
                ToJson: """["invitee@example.com"]""",
                CcJson: null,
                BodyPreview: "Secret agenda",
                ProtectedFieldsAvailable: true,
                IsRedacted: false,
                SenderEmailResolved: "organizer.resolved@example.com",
                FromEmailAddress: "organizer@example.com",
                ConversationId: "conv-2",
                MeetingMessageType: 1
            ),
            // All protected fields already null, mail kind, Sensitivity 3.
            new MessageDto(
                BridgeId: "olk:msg:mail-null-protected",
                ItemKind: "mail",
                Subject: null,
                ReceivedUtc: null,
                SentUtc: null,
                Importance: null,
                Sensitivity: 3,
                Unread: false,
                HasAttachments: true,
                MessageClass: null,
                SenderName: null,
                SenderEmail: null,
                ToJson: null,
                CcJson: null,
                BodyPreview: null,
                ProtectedFieldsAvailable: false,
                IsRedacted: false,
                SenderEmailResolved: null,
                FromEmailAddress: null,
                ConversationId: null,
                MeetingMessageType: null
            ),
            // All protected fields already null, meeting kind, Sensitivity 2, flags inverted.
            new MessageDto(
                BridgeId: "olk:msg:meeting-null-protected",
                ItemKind: "meeting",
                Subject: null,
                ReceivedUtc: FixedReceived.AddDays(-1),
                SentUtc: null,
                Importance: 0,
                Sensitivity: 2,
                Unread: true,
                HasAttachments: false,
                MessageClass: "IPM.Schedule.Meeting.Canceled",
                SenderName: null,
                SenderEmail: null,
                ToJson: null,
                CcJson: null,
                BodyPreview: null,
                ProtectedFieldsAvailable: false,
                IsRedacted: true,
                SenderEmailResolved: null,
                FromEmailAddress: null,
                ConversationId: "conv-3",
                MeetingMessageType: 3
            ),
        ];

    [TestMethod]
    public void RedactMessage_should_transform_exactly_the_protected_set_for_every_variant()
    {
        foreach (var input in MessageMatrix())
        {
            var redacted = OutlookScanner.RedactMessage(input);

            redacted.Subject.Should().Be("Private message", "variant {0}", input.BridgeId);
            redacted.SenderName.Should().BeNull("variant {0}", input.BridgeId);
            redacted.SenderEmail.Should().BeNull("variant {0}", input.BridgeId);
            redacted.SenderEmailResolved.Should().BeNull("variant {0}", input.BridgeId);
            redacted.FromEmailAddress.Should().BeNull("variant {0}", input.BridgeId);
            redacted.ToJson.Should().BeNull("variant {0}", input.BridgeId);
            redacted.CcJson.Should().BeNull("variant {0}", input.BridgeId);
            redacted.BodyPreview.Should().BeNull("variant {0}", input.BridgeId);
            redacted.IsRedacted.Should().BeTrue("variant {0}", input.BridgeId);
            redacted.ProtectedFieldsAvailable.Should().BeFalse("variant {0}", input.BridgeId);
        }
    }

    [TestMethod]
    public void RedactMessage_should_preserve_every_mechanical_field_for_every_variant()
    {
        foreach (var input in MessageMatrix())
        {
            var redacted = OutlookScanner.RedactMessage(input);

            redacted.BridgeId.Should().Be(input.BridgeId);
            redacted.ItemKind.Should().Be(input.ItemKind, "variant {0}", input.BridgeId);
            redacted.MessageClass.Should().Be(input.MessageClass, "variant {0}", input.BridgeId);
            redacted.ReceivedUtc.Should().Be(input.ReceivedUtc, "variant {0}", input.BridgeId);
            redacted.SentUtc.Should().Be(input.SentUtc, "variant {0}", input.BridgeId);
            redacted.Importance.Should().Be(input.Importance, "variant {0}", input.BridgeId);
            redacted.Sensitivity.Should().Be(input.Sensitivity, "variant {0}", input.BridgeId);
            redacted.Unread.Should().Be(input.Unread, "variant {0}", input.BridgeId);
            redacted
                .HasAttachments.Should()
                .Be(input.HasAttachments, "variant {0}", input.BridgeId);
            redacted
                .ConversationId.Should()
                .Be(input.ConversationId, "variant {0}", input.BridgeId);
            redacted
                .MeetingMessageType.Should()
                .Be(input.MeetingMessageType, "variant {0}", input.BridgeId);
        }
    }

    [TestMethod]
    public void RedactMessage_should_be_idempotent_for_every_variant()
    {
        foreach (var input in MessageMatrix())
        {
            var once = OutlookScanner.RedactMessage(input);
            var twice = OutlookScanner.RedactMessage(once);

            twice.Should().BeEquivalentTo(once, "variant {0}", input.BridgeId);
        }
    }

    private static EventDto[] EventMatrix() =>
        [
            // Populated protected fields, Sensitivity 2, populated Categories.
            new EventDto(
                BridgeId: "olk:evt:populated-2",
                GlobalAppointmentId: "gid-1",
                Subject: "Secret review",
                StartUtc: FixedStart,
                EndUtc: FixedStart.AddHours(1),
                Location: "Secret Room",
                BusyStatus: 2,
                MeetingStatus: 1,
                IsRecurring: true,
                Sensitivity: 2,
                Organizer: "Secret Organizer",
                RequiredAttendeesJson: """["a@example.com"]""",
                OptionalAttendeesJson: """["b@example.com"]""",
                ResourcesJson: """["room@example.com"]""",
                BodyPreview: "Secret agenda",
                ProtectedFieldsAvailable: true,
                IsRedacted: false,
                ResponseStatus: 1,
                Categories: ["Red category", "Blue category"],
                IsOrganizer: true,
                IsOnlineMeeting: true,
                AllowNewTimeProposals: true,
                ICalUId: "gid-1",
                SeriesMasterId: "gid-1",
                LastModifiedDateTime: FixedStart.AddDays(-1),
                BodyFull: "Secret agenda full",
                SensitivityLabel: "private"
            ),
            // Populated protected fields, Sensitivity 3, empty Categories.
            new EventDto(
                BridgeId: "olk:evt:populated-3",
                GlobalAppointmentId: "gid-2",
                Subject: "Secret confidential sync",
                StartUtc: FixedStart.AddDays(1),
                EndUtc: FixedStart.AddDays(1).AddMinutes(30),
                Location: "Secret Annex",
                BusyStatus: 1,
                MeetingStatus: 5,
                IsRecurring: false,
                Sensitivity: 3,
                Organizer: "Secret Chair",
                RequiredAttendeesJson: """["c@example.com"]""",
                OptionalAttendeesJson: null,
                ResourcesJson: null,
                BodyPreview: "Secret notes",
                ProtectedFieldsAvailable: true,
                IsRedacted: false,
                ResponseStatus: 3,
                Categories: [],
                IsOrganizer: false,
                IsOnlineMeeting: false,
                AllowNewTimeProposals: false,
                ICalUId: "gid-2",
                SeriesMasterId: null,
                LastModifiedDateTime: null,
                BodyFull: "Secret notes full",
                SensitivityLabel: "confidential"
            ),
            // Protected fields already null, Sensitivity 2, empty Categories.
            new EventDto(
                BridgeId: "olk:evt:null-protected-2",
                GlobalAppointmentId: null,
                Subject: null,
                StartUtc: FixedStart.AddDays(2),
                EndUtc: FixedStart.AddDays(2).AddHours(2),
                Location: null,
                BusyStatus: null,
                MeetingStatus: null,
                IsRecurring: false,
                Sensitivity: 2,
                Organizer: null,
                RequiredAttendeesJson: null,
                OptionalAttendeesJson: null,
                ResourcesJson: null,
                BodyPreview: null,
                ProtectedFieldsAvailable: false,
                IsRedacted: true,
                ResponseStatus: null,
                Categories: [],
                IsOrganizer: false,
                IsOnlineMeeting: false,
                AllowNewTimeProposals: false,
                ICalUId: null,
                SeriesMasterId: null,
                LastModifiedDateTime: null,
                BodyFull: null,
                SensitivityLabel: "private"
            ),
            // Protected fields already null, Sensitivity 3, populated Categories.
            new EventDto(
                BridgeId: "olk:evt:null-protected-3",
                GlobalAppointmentId: "gid-4",
                Subject: null,
                StartUtc: FixedStart.AddDays(3),
                EndUtc: FixedStart.AddDays(3).AddHours(1),
                Location: null,
                BusyStatus: 3,
                MeetingStatus: 0,
                IsRecurring: true,
                Sensitivity: 3,
                Organizer: null,
                RequiredAttendeesJson: null,
                OptionalAttendeesJson: null,
                ResourcesJson: null,
                BodyPreview: null,
                ProtectedFieldsAvailable: false,
                IsRedacted: false,
                ResponseStatus: 2,
                Categories: ["Leftover category"],
                IsOrganizer: true,
                IsOnlineMeeting: true,
                AllowNewTimeProposals: true,
                ICalUId: "gid-4",
                SeriesMasterId: "gid-4",
                LastModifiedDateTime: FixedStart,
                BodyFull: null,
                SensitivityLabel: "confidential"
            ),
        ];

    [TestMethod]
    public void RedactEvent_should_transform_exactly_the_protected_set_for_every_variant()
    {
        foreach (var input in EventMatrix())
        {
            var redacted = OutlookScanner.RedactEvent(input);

            redacted.Subject.Should().Be("Private appointment", "variant {0}", input.BridgeId);
            redacted.Location.Should().BeNull("variant {0}", input.BridgeId);
            redacted.Organizer.Should().BeNull("variant {0}", input.BridgeId);
            redacted.RequiredAttendeesJson.Should().BeNull("variant {0}", input.BridgeId);
            redacted.OptionalAttendeesJson.Should().BeNull("variant {0}", input.BridgeId);
            redacted.ResourcesJson.Should().BeNull("variant {0}", input.BridgeId);
            redacted.BodyPreview.Should().BeNull("variant {0}", input.BridgeId);
            redacted.BodyFull.Should().BeNull("variant {0}", input.BridgeId);
            redacted.Categories.Should().NotBeNull("variant {0}", input.BridgeId);
            redacted.Categories.Should().BeEmpty("variant {0}", input.BridgeId);
            redacted.IsRedacted.Should().BeTrue("variant {0}", input.BridgeId);
            redacted.ProtectedFieldsAvailable.Should().BeFalse("variant {0}", input.BridgeId);
        }
    }

    [TestMethod]
    public void RedactEvent_should_preserve_every_mechanical_field_for_every_variant()
    {
        foreach (var input in EventMatrix())
        {
            var redacted = OutlookScanner.RedactEvent(input);

            redacted.BridgeId.Should().Be(input.BridgeId);
            redacted
                .GlobalAppointmentId.Should()
                .Be(input.GlobalAppointmentId, "variant {0}", input.BridgeId);
            redacted.StartUtc.Should().Be(input.StartUtc, "variant {0}", input.BridgeId);
            redacted.EndUtc.Should().Be(input.EndUtc, "variant {0}", input.BridgeId);
            redacted.BusyStatus.Should().Be(input.BusyStatus, "variant {0}", input.BridgeId);
            redacted.MeetingStatus.Should().Be(input.MeetingStatus, "variant {0}", input.BridgeId);
            redacted.IsRecurring.Should().Be(input.IsRecurring, "variant {0}", input.BridgeId);
            redacted.Sensitivity.Should().Be(input.Sensitivity, "variant {0}", input.BridgeId);
            redacted
                .SensitivityLabel.Should()
                .Be(input.SensitivityLabel, "variant {0}", input.BridgeId);
            redacted
                .ResponseStatus.Should()
                .Be(input.ResponseStatus, "variant {0}", input.BridgeId);
            redacted.IsOrganizer.Should().Be(input.IsOrganizer, "variant {0}", input.BridgeId);
            redacted
                .IsOnlineMeeting.Should()
                .Be(input.IsOnlineMeeting, "variant {0}", input.BridgeId);
            redacted
                .AllowNewTimeProposals.Should()
                .Be(input.AllowNewTimeProposals, "variant {0}", input.BridgeId);
            redacted.ICalUId.Should().Be(input.ICalUId, "variant {0}", input.BridgeId);
            redacted
                .SeriesMasterId.Should()
                .Be(input.SeriesMasterId, "variant {0}", input.BridgeId);
            redacted
                .LastModifiedDateTime.Should()
                .Be(input.LastModifiedDateTime, "variant {0}", input.BridgeId);
        }
    }

    [TestMethod]
    public void RedactEvent_should_be_idempotent_for_every_variant()
    {
        foreach (var input in EventMatrix())
        {
            var once = OutlookScanner.RedactEvent(input);
            var twice = OutlookScanner.RedactEvent(once);

            twice.Should().BeEquivalentTo(once, "variant {0}", input.BridgeId);
        }
    }
}
