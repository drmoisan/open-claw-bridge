using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Unit tests for the pure sensitivity-redaction transforms in
/// <c>OutlookScanner.Redaction.cs</c> (issue #18): the <c>IsSensitive</c> boundary contract and
/// the full field disposition of the message and event redaction transforms.
/// </summary>
[TestClass]
public class OutlookScannerRedactionTests
{
    private static readonly DateTimeOffset FixedReceived = DateTimeOffset.Parse(
        "2026-07-01T09:00:00Z"
    );
    private static readonly DateTimeOffset FixedSent = DateTimeOffset.Parse("2026-07-01T08:55:00Z");
    private static readonly DateTimeOffset FixedStart = DateTimeOffset.Parse(
        "2026-07-03T14:00:00Z"
    );
    private static readonly DateTimeOffset FixedEnd = DateTimeOffset.Parse("2026-07-03T15:00:00Z");
    private static readonly DateTimeOffset FixedModified = DateTimeOffset.Parse(
        "2026-06-30T18:30:00Z"
    );

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public void IsSensitive_should_be_true_for_private_and_confidential(int sensitivity)
    {
        OutlookScanner.IsSensitive(sensitivity).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(null)]
    [DataRow(-1)]
    [DataRow(4)]
    [DataRow(99)]
    public void IsSensitive_should_be_false_for_non_sensitive_and_out_of_range(int? sensitivity)
    {
        OutlookScanner.IsSensitive(sensitivity).Should().BeFalse();
    }

    [TestMethod]
    public void RedactMessage_should_replace_subject_null_protected_fields_and_set_flags()
    {
        var message = CreateFullMessage(sensitivity: 2);

        var redacted = OutlookScanner.RedactMessage(message);

        redacted.Subject.Should().Be("Private message");
        redacted.SenderName.Should().BeNull();
        redacted.SenderEmail.Should().BeNull();
        redacted.SenderEmailResolved.Should().BeNull();
        redacted.FromEmailAddress.Should().BeNull();
        redacted.ToJson.Should().BeNull();
        redacted.CcJson.Should().BeNull();
        redacted.BodyPreview.Should().BeNull();
        redacted.IsRedacted.Should().BeTrue();
        redacted.ProtectedFieldsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void RedactMessage_should_retain_every_mechanical_field_unchanged()
    {
        var message = CreateFullMessage(sensitivity: 3);

        var redacted = OutlookScanner.RedactMessage(message);

        redacted.BridgeId.Should().Be(message.BridgeId);
        redacted.ItemKind.Should().Be(message.ItemKind);
        redacted.MessageClass.Should().Be(message.MessageClass);
        redacted.ReceivedUtc.Should().Be(message.ReceivedUtc);
        redacted.SentUtc.Should().Be(message.SentUtc);
        redacted.Importance.Should().Be(message.Importance);
        redacted.Sensitivity.Should().Be(message.Sensitivity);
        redacted.Unread.Should().Be(message.Unread);
        redacted.HasAttachments.Should().Be(message.HasAttachments);
        redacted.ConversationId.Should().Be(message.ConversationId);
        redacted.MeetingMessageType.Should().Be(message.MeetingMessageType);
    }

    [TestMethod]
    public void RedactEvent_should_replace_subject_null_protected_fields_and_set_flags()
    {
        var evt = CreateFullEvent(sensitivity: 2);

        var redacted = OutlookScanner.RedactEvent(evt);

        redacted.Subject.Should().Be("Private appointment");
        redacted.Location.Should().BeNull();
        redacted.Organizer.Should().BeNull();
        redacted.RequiredAttendeesJson.Should().BeNull();
        redacted.OptionalAttendeesJson.Should().BeNull();
        redacted.ResourcesJson.Should().BeNull();
        redacted.BodyPreview.Should().BeNull();
        redacted.BodyFull.Should().BeNull();
        redacted.Categories.Should().NotBeNull();
        redacted.Categories.Should().BeEmpty();
        redacted.IsRedacted.Should().BeTrue();
        redacted.ProtectedFieldsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void RedactEvent_should_retain_every_mechanical_field_unchanged()
    {
        var evt = CreateFullEvent(sensitivity: 3);

        var redacted = OutlookScanner.RedactEvent(evt);

        redacted.BridgeId.Should().Be(evt.BridgeId);
        redacted.GlobalAppointmentId.Should().Be(evt.GlobalAppointmentId);
        redacted.StartUtc.Should().Be(evt.StartUtc);
        redacted.EndUtc.Should().Be(evt.EndUtc);
        redacted.BusyStatus.Should().Be(evt.BusyStatus);
        redacted.MeetingStatus.Should().Be(evt.MeetingStatus);
        redacted.IsRecurring.Should().Be(evt.IsRecurring);
        redacted.Sensitivity.Should().Be(evt.Sensitivity);
        redacted.SensitivityLabel.Should().Be(evt.SensitivityLabel);
        redacted.ResponseStatus.Should().Be(evt.ResponseStatus);
        redacted.IsOrganizer.Should().Be(evt.IsOrganizer);
        redacted.IsOnlineMeeting.Should().Be(evt.IsOnlineMeeting);
        redacted.AllowNewTimeProposals.Should().Be(evt.AllowNewTimeProposals);
        redacted.ICalUId.Should().Be(evt.ICalUId);
        redacted.SeriesMasterId.Should().Be(evt.SeriesMasterId);
        redacted.LastModifiedDateTime.Should().Be(evt.LastModifiedDateTime);
    }

    private static MessageDto CreateFullMessage(int sensitivity) =>
        new(
            BridgeIdCodec.MessageId("entry-redact-1", true),
            "meeting",
            "Discussion with HR — retention offer",
            FixedReceived,
            FixedSent,
            2,
            sensitivity,
            true,
            true,
            "IPM.Schedule.Meeting.Request",
            "HR Director",
            "hr@example.com",
            "[{\"name\":\"Dan\",\"email\":\"dan@example.com\"}]",
            "[{\"name\":\"Cc Person\",\"email\":\"cc@example.com\"}]",
            "Compensation details preview",
            true,
            false,
            SenderEmailResolved: "hr.director@corp.example.com",
            FromEmailAddress: "hr-noreply@corp.example.com",
            ConversationId: "conv-42",
            MeetingMessageType: 1
        );

    private static EventDto CreateFullEvent(int sensitivity) =>
        new(
            BridgeIdCodec.EventId("gid-redact-1", "entry-redact-2", FixedStart),
            "gid-redact-1",
            "Discussion with HR — retention offer",
            FixedStart,
            FixedEnd,
            "HR Conference Room",
            2,
            1,
            true,
            sensitivity,
            "HR Director",
            "[{\"name\":\"Dan\",\"email\":\"dan@example.com\"}]",
            "[{\"name\":\"Optional\",\"email\":\"opt@example.com\"}]",
            "[{\"name\":\"Projector\",\"email\":\"proj@example.com\"}]",
            "Agenda preview",
            true,
            false,
            ResponseStatus: 1,
            Categories: new[] { "HR", "Compensation" },
            IsOrganizer: true,
            IsOnlineMeeting: true,
            AllowNewTimeProposals: true,
            ICalUId: "gid-redact-1",
            SeriesMasterId: "gid-redact-1",
            LastModifiedDateTime: FixedModified,
            BodyFull: "Full body with confidential content",
            SensitivityLabel: EventSensitivityLabel.FromSensitivity(sensitivity)
        );
}
