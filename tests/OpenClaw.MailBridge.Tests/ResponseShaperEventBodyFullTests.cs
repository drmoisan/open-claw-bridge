using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Redaction tests for issue #72 (AC3): <see cref="ResponseShaper.ShapeEvent"/> must null
/// <see cref="EventDto.BodyFull"/> in safe mode (parity with <c>BodyPreview</c>), and return the
/// full untruncated body verbatim in enhanced mode (NOT routed through
/// <c>BodySanitizer.NormalizePreview</c>).
/// </summary>
[TestClass]
public sealed class ResponseShaperEventBodyFullTests
{
    private static readonly DateTimeOffset FixedTimestamp = DateTimeOffset.Parse(
        "2026-05-01T12:00:00Z"
    );

    private static EventDto CreateEvent(string? bodyFull, string? bodyPreview = "Preview") =>
        new(
            BridgeId: BridgeIdCodec.EventId("gid", "entry-bodyfull", FixedTimestamp),
            GlobalAppointmentId: "gid",
            Subject: "Subject",
            StartUtc: FixedTimestamp,
            EndUtc: FixedTimestamp.AddHours(1),
            Location: "Room 1",
            BusyStatus: null,
            MeetingStatus: null,
            IsRecurring: false,
            Sensitivity: null,
            Organizer: "Organizer",
            RequiredAttendeesJson: null,
            OptionalAttendeesJson: null,
            ResourcesJson: null,
            BodyPreview: bodyPreview,
            ProtectedFieldsAvailable: true,
            IsRedacted: false,
            BodyFull: bodyFull
        );

    [TestMethod]
    public void ShapeEvent_in_safe_mode_should_null_body_full_and_preserve_is_redacted()
    {
        // Arrange
        var evt = CreateEvent(bodyFull: "Full confidential appointment body text.");
        var settings = BridgeSettings.Default with { Mode = "safe" };

        // Act
        var shaped = ResponseShaper.ShapeEvent(evt, settings);

        // Assert
        shaped.BodyFull.Should().BeNull("safe mode must null bodyFull alongside BodyPreview");
        shaped.BodyPreview.Should().BeNull();
        // Deliberate change (#18 x #20 conflation fix): IsRedacted is now exclusively the
        // sensitivity-redaction signal; safe-mode suppression is signaled by
        // ProtectedFieldsAvailable = false, so the shaper must preserve the input false value.
        shaped.IsRedacted.Should().BeFalse();
    }

    private static EventDto CreateEventWithAttendees() =>
        CreateEvent(bodyFull: "Body") with
        {
            RequiredAttendeesJson = "[{\"name\":\"Req\",\"email\":\"req@example.com\"}]",
            OptionalAttendeesJson = "[{\"name\":\"Opt\",\"email\":\"opt@example.com\"}]",
            ResourcesJson = "[{\"name\":\"Room\",\"email\":\"room@example.com\"}]",
        };

    [TestMethod]
    public void ShapeEvent_in_safe_mode_should_null_all_three_attendee_fields()
    {
        // Arrange (issue #71 US-AC4): populated attendee JSON fields must be redacted in safe mode,
        // matching the message-path redaction of SenderName/SenderEmail.
        var evt = CreateEventWithAttendees();
        var settings = BridgeSettings.Default with { Mode = "safe" };

        // Act
        var shaped = ResponseShaper.ShapeEvent(evt, settings);

        // Assert
        shaped.RequiredAttendeesJson.Should().BeNull("safe mode must redact attendee PII");
        shaped.OptionalAttendeesJson.Should().BeNull("safe mode must redact attendee PII");
        shaped.ResourcesJson.Should().BeNull("safe mode must redact attendee PII");
        // Deliberate change (#18 x #20 conflation fix): IsRedacted is now exclusively the
        // sensitivity-redaction signal; safe-mode suppression is signaled by
        // ProtectedFieldsAvailable = false, so the shaper must preserve the input false value.
        shaped.IsRedacted.Should().BeFalse();
    }

    [TestMethod]
    public void ShapeEvent_in_enhanced_mode_should_preserve_all_three_attendee_fields()
    {
        // Arrange (issue #71 US-AC4 non-regression): enhanced mode leaves attendee JSON intact.
        var evt = CreateEventWithAttendees();
        var settings = BridgeSettings.Default with { Mode = "enhanced" };

        // Act
        var shaped = ResponseShaper.ShapeEvent(evt, settings);

        // Assert
        shaped.RequiredAttendeesJson.Should().Be(evt.RequiredAttendeesJson);
        shaped.OptionalAttendeesJson.Should().Be(evt.OptionalAttendeesJson);
        shaped.ResourcesJson.Should().Be(evt.ResourcesJson);
        shaped.IsRedacted.Should().BeFalse();
    }

    [TestMethod]
    public void ShapeEvent_in_enhanced_mode_should_return_full_untruncated_body_verbatim()
    {
        // Arrange: a body longer than the preview cap to prove BodyFull is not truncated.
        var settings = BridgeSettings.Default with
        {
            Mode = "enhanced",
            BodyPreviewMaxChars = 16,
        };
        var longBody =
            "First line with multiple words.\n"
            + new string('a', settings.BodyPreviewMaxChars + 200)
            + "  trailing   whitespace   preserved";
        var evt = CreateEvent(bodyFull: longBody, bodyPreview: longBody);

        // Act
        var shaped = ResponseShaper.ShapeEvent(evt, settings);

        // Assert
        shaped
            .BodyFull.Should()
            .Be(longBody, "enhanced mode returns the raw body verbatim, not normalized/truncated");
        shaped.BodyFull!.Length.Should().BeGreaterThan(settings.BodyPreviewMaxChars);
        shaped.IsRedacted.Should().BeFalse();
        // BodyPreview is still shaped/truncated; BodyFull diverges from it intentionally.
        shaped.BodyFull.Should().NotBe(shaped.BodyPreview);
    }
}
