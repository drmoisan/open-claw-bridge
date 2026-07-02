using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Safe-mode suppression-completion tests for issue #20 (spec Group B): safe-mode shaping must
/// suppress the full protected field set and set <c>ProtectedFieldsAvailable = false</c>, retain
/// every mechanical field (including event <c>Location</c>), pass everything through in enhanced
/// mode, and shape already-null fields without error.
/// </summary>
[TestClass]
public sealed class ResponseShaperSafeModeSuppressionTests
{
    private static readonly DateTimeOffset FixedReceived = DateTimeOffset.Parse(
        "2026-07-01T09:00:00Z"
    );
    private static readonly DateTimeOffset FixedStart = DateTimeOffset.Parse(
        "2026-07-03T14:00:00Z"
    );
    private static readonly DateTimeOffset FixedModified = DateTimeOffset.Parse(
        "2026-06-30T18:30:00Z"
    );

    private static BridgeSettings SafeSettings => BridgeSettings.Default with { Mode = "safe" };

    private static BridgeSettings EnhancedSettings =>
        BridgeSettings.Default with
        {
            Mode = "enhanced",
        };

    private static MessageDto CreateFullMessage() =>
        new(
            BridgeIdCodec.MessageId("entry-suppress-1", true),
            "meeting",
            "Quarterly report",
            FixedReceived,
            FixedReceived.AddMinutes(-10),
            2,
            1,
            true,
            true,
            "IPM.Schedule.Meeting.Request",
            "Sender Name",
            "sender@example.com",
            "[{\"name\":\"To\",\"email\":\"to@example.com\"}]",
            "[{\"name\":\"Cc\",\"email\":\"cc@example.com\"}]",
            "Preview text",
            true,
            false,
            SenderEmailResolved: "sender.resolved@example.com",
            FromEmailAddress: "from@example.com",
            ConversationId: "conv-suppress",
            MeetingMessageType: 1
        );

    private static EventDto CreateFullEvent() =>
        new(
            BridgeIdCodec.EventId("gid-suppress-1", "entry-suppress-2", FixedStart),
            "gid-suppress-1",
            "Planning session",
            FixedStart,
            FixedStart.AddHours(1),
            "Conference Room A",
            2,
            1,
            true,
            1,
            "Organizer Name",
            "[{\"name\":\"Req\",\"email\":\"req@example.com\"}]",
            "[{\"name\":\"Opt\",\"email\":\"opt@example.com\"}]",
            "[{\"name\":\"Room\",\"email\":\"room@example.com\"}]",
            "Agenda preview",
            true,
            false,
            ResponseStatus: 1,
            Categories: new[] { "Planning", "Team" },
            IsOrganizer: true,
            IsOnlineMeeting: true,
            AllowNewTimeProposals: true,
            ICalUId: "gid-suppress-1",
            SeriesMasterId: "gid-suppress-1",
            LastModifiedDateTime: FixedModified,
            BodyFull: "Full agenda body",
            SensitivityLabel: null
        );

    [TestMethod]
    public void ShapeMessage_safe_mode_should_suppress_full_protected_field_set()
    {
        // B1: the extended suppression set plus the pre-existing three fields.
        var shaped = ResponseShaper.ShapeMessage(CreateFullMessage(), SafeSettings);

        shaped.ToJson.Should().BeNull();
        shaped.CcJson.Should().BeNull();
        shaped.SenderEmailResolved.Should().BeNull();
        shaped.FromEmailAddress.Should().BeNull();
        shaped.BodyPreview.Should().BeNull();
        shaped.SenderName.Should().BeNull();
        shaped.SenderEmail.Should().BeNull();
        shaped.ProtectedFieldsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void ShapeMessage_safe_mode_should_retain_all_mechanical_fields()
    {
        // B2: every non-protected message field passes through unchanged.
        var message = CreateFullMessage();

        var shaped = ResponseShaper.ShapeMessage(message, SafeSettings);

        shaped.BridgeId.Should().Be(message.BridgeId);
        shaped.ItemKind.Should().Be(message.ItemKind);
        shaped.Subject.Should().Be(message.Subject);
        shaped.ReceivedUtc.Should().Be(message.ReceivedUtc);
        shaped.SentUtc.Should().Be(message.SentUtc);
        shaped.Importance.Should().Be(message.Importance);
        shaped.Sensitivity.Should().Be(message.Sensitivity);
        shaped.Unread.Should().Be(message.Unread);
        shaped.HasAttachments.Should().Be(message.HasAttachments);
        shaped.MessageClass.Should().Be(message.MessageClass);
        shaped.ConversationId.Should().Be(message.ConversationId);
        shaped.MeetingMessageType.Should().Be(message.MeetingMessageType);
    }

    [TestMethod]
    public void ShapeEvent_safe_mode_should_suppress_organizer_categories_and_set_flag()
    {
        // B3: Organizer joins the five pre-existing suppressed fields; Categories empties;
        // ProtectedFieldsAvailable is forced false.
        var shaped = ResponseShaper.ShapeEvent(CreateFullEvent(), SafeSettings);

        shaped.Organizer.Should().BeNull();
        shaped.BodyPreview.Should().BeNull();
        shaped.BodyFull.Should().BeNull();
        shaped.RequiredAttendeesJson.Should().BeNull();
        shaped.OptionalAttendeesJson.Should().BeNull();
        shaped.ResourcesJson.Should().BeNull();
        shaped.Categories.Should().NotBeNull();
        shaped.Categories.Should().BeEmpty();
        shaped.ProtectedFieldsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void ShapeEvent_safe_mode_should_retain_location_and_all_mechanical_fields()
    {
        // B4: Location is retained in safe mode (decided behavior, spec section B); all
        // mechanical fields pass through unchanged.
        var evt = CreateFullEvent();

        var shaped = ResponseShaper.ShapeEvent(evt, SafeSettings);

        shaped.Location.Should().Be("Conference Room A");
        shaped.BridgeId.Should().Be(evt.BridgeId);
        shaped.GlobalAppointmentId.Should().Be(evt.GlobalAppointmentId);
        shaped.Subject.Should().Be(evt.Subject);
        shaped.StartUtc.Should().Be(evt.StartUtc);
        shaped.EndUtc.Should().Be(evt.EndUtc);
        shaped.BusyStatus.Should().Be(evt.BusyStatus);
        shaped.MeetingStatus.Should().Be(evt.MeetingStatus);
        shaped.IsRecurring.Should().Be(evt.IsRecurring);
        shaped.Sensitivity.Should().Be(evt.Sensitivity);
        shaped.SensitivityLabel.Should().Be(evt.SensitivityLabel);
        shaped.ResponseStatus.Should().Be(evt.ResponseStatus);
        shaped.IsOrganizer.Should().Be(evt.IsOrganizer);
        shaped.IsOnlineMeeting.Should().Be(evt.IsOnlineMeeting);
        shaped.AllowNewTimeProposals.Should().Be(evt.AllowNewTimeProposals);
        shaped.ICalUId.Should().Be(evt.ICalUId);
        shaped.SeriesMasterId.Should().Be(evt.SeriesMasterId);
        shaped.LastModifiedDateTime.Should().Be(evt.LastModifiedDateTime);
    }

    [TestMethod]
    public void Enhanced_mode_should_pass_through_all_fields_without_forcing_flag()
    {
        // B5: enhanced mode nulls nothing and does not force ProtectedFieldsAvailable = false;
        // BodyPreview is still sanitized/truncated and BodyFull returns verbatim.
        var settings = EnhancedSettings with
        {
            BodyPreviewMaxChars = 7,
        };
        var message = CreateFullMessage();
        var evt = CreateFullEvent();

        var shapedMessage = ResponseShaper.ShapeMessage(message, settings);
        var shapedEvent = ResponseShaper.ShapeEvent(evt, settings);

        shapedMessage.SenderName.Should().Be(message.SenderName);
        shapedMessage.SenderEmail.Should().Be(message.SenderEmail);
        shapedMessage.ToJson.Should().Be(message.ToJson);
        shapedMessage.CcJson.Should().Be(message.CcJson);
        shapedMessage.SenderEmailResolved.Should().Be(message.SenderEmailResolved);
        shapedMessage.FromEmailAddress.Should().Be(message.FromEmailAddress);
        shapedMessage.ProtectedFieldsAvailable.Should().BeTrue();
        shapedMessage.BodyPreview.Should().Be("Preview", "preview is sanitized/truncated");
        shapedEvent.Organizer.Should().Be(evt.Organizer);
        shapedEvent.RequiredAttendeesJson.Should().Be(evt.RequiredAttendeesJson);
        shapedEvent.OptionalAttendeesJson.Should().Be(evt.OptionalAttendeesJson);
        shapedEvent.ResourcesJson.Should().Be(evt.ResourcesJson);
        shapedEvent.Categories.Should().Equal("Planning", "Team");
        shapedEvent.Location.Should().Be(evt.Location);
        shapedEvent.ProtectedFieldsAvailable.Should().BeTrue();
        shapedEvent.BodyFull.Should().Be("Full agenda body", "bodyFull returns verbatim");
    }

    [TestMethod]
    public void Already_null_protected_fields_should_shape_without_error_in_both_modes()
    {
        // B6: a DTO whose protected fields are already null shapes without error.
        var message = CreateFullMessage() with
        {
            SenderName = null,
            SenderEmail = null,
            SenderEmailResolved = null,
            FromEmailAddress = null,
            ToJson = null,
            CcJson = null,
            BodyPreview = null,
        };
        var evt = CreateFullEvent() with
        {
            Organizer = null,
            RequiredAttendeesJson = null,
            OptionalAttendeesJson = null,
            ResourcesJson = null,
            BodyPreview = null,
            BodyFull = null,
        };

        var safeMessage = () => ResponseShaper.ShapeMessage(message, SafeSettings);
        var enhancedMessage = () => ResponseShaper.ShapeMessage(message, EnhancedSettings);
        var safeEvent = () => ResponseShaper.ShapeEvent(evt, SafeSettings);
        var enhancedEvent = () => ResponseShaper.ShapeEvent(evt, EnhancedSettings);

        safeMessage.Should().NotThrow();
        enhancedMessage.Should().NotThrow();
        safeEvent.Should().NotThrow();
        enhancedEvent.Should().NotThrow();
    }
}
