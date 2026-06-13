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
    public void ShapeEvent_in_safe_mode_should_null_body_full_and_set_redacted()
    {
        // Arrange
        var evt = CreateEvent(bodyFull: "Full confidential appointment body text.");
        var settings = BridgeSettings.Default with { Mode = "safe" };

        // Act
        var shaped = ResponseShaper.ShapeEvent(evt, settings);

        // Assert
        shaped.BodyFull.Should().BeNull("safe mode must null bodyFull alongside BodyPreview");
        shaped.BodyPreview.Should().BeNull();
        shaped.IsRedacted.Should().BeTrue();
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
