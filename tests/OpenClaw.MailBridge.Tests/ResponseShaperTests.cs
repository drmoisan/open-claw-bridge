using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class ResponseShaperTests
{
    private static readonly DateTimeOffset FixedTimestamp = DateTimeOffset.Parse(
        "2026-04-08T12:00:00Z"
    );

    [TestMethod]
    public void ShapeMessage_in_safe_mode_should_redact_sender_fields_and_clear_preview()
    {
        var message = CreateMessage(bodyPreview: "Preview text");
        var settings = BridgeSettings.Default with { Mode = "safe" };

        var shaped = ResponseShaper.ShapeMessage(message, settings);

        shaped.BodyPreview.Should().BeNull();
        shaped.SenderName.Should().BeNull();
        shaped.SenderEmail.Should().BeNull();
        shaped.IsRedacted.Should().BeTrue();
        shaped.Subject.Should().Be(message.Subject);
    }

    [TestMethod]
    public void ShapeMessage_in_enhanced_mode_should_keep_sanitized_preview_and_not_redact()
    {
        var message = CreateMessage(
            senderName: "Sender",
            senderEmail: "sender@example.com",
            bodyPreview: "<b>Hello</b> C:\\secret\\file.txt and trailing"
        );
        var settings = BridgeSettings.Default with { Mode = "EnHaNcEd", BodyPreviewMaxChars = 12 };

        var shaped = ResponseShaper.ShapeMessage(message, settings);

        shaped.BodyPreview.Should().Be("Hello [path]");
        shaped.SenderName.Should().Be(message.SenderName);
        shaped.SenderEmail.Should().Be(message.SenderEmail);
        shaped.IsRedacted.Should().BeFalse();
    }

    [TestMethod]
    public void ShapeEvent_in_safe_mode_should_clear_preview_and_redact()
    {
        var evt = CreateEvent(bodyPreview: "Event preview");
        var settings = BridgeSettings.Default with { Mode = "safe" };

        var shaped = ResponseShaper.ShapeEvent(evt, settings);

        shaped.BodyPreview.Should().BeNull();
        shaped.IsRedacted.Should().BeTrue();
        shaped.Subject.Should().Be(evt.Subject);
    }

    [TestMethod]
    public void ShapeEvent_in_enhanced_mode_should_keep_sanitized_preview_and_not_redact()
    {
        var evt = CreateEvent(bodyPreview: "<i>Agenda</i> C:\\temp\\agenda.docx and more");
        var settings = BridgeSettings.Default with { Mode = "enhanced", BodyPreviewMaxChars = 13 };

        var shaped = ResponseShaper.ShapeEvent(evt, settings);

        shaped.BodyPreview.Should().Be("Agenda [path]");
        shaped.IsRedacted.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ShapePreview_should_return_null_for_missing_text(string? preview)
    {
        var settings = BridgeSettings.Default;

        var shaped = ResponseShaper.ShapePreview(preview, settings);

        shaped.Should().BeNull();
    }

    [TestMethod]
    public void ShapePreview_should_sanitize_and_truncate_preview_text()
    {
        var settings = BridgeSettings.Default with { BodyPreviewMaxChars = 12 };

        var shaped = ResponseShaper.ShapePreview(
            "<b>Hello</b> C:\\secret\\file.txt and trailing",
            settings
        );

        shaped.Should().Be("Hello [path]");
    }

    private static MessageDto CreateMessage(
        string? senderName = "Sender",
        string? senderEmail = "sender@example.com",
        string? bodyPreview = "Preview"
    ) =>
        new(
            BridgeIdCodec.MessageId("entry-1", false),
            "mail",
            "Subject",
            FixedTimestamp,
            null,
            null,
            null,
            false,
            false,
            "IPM.Note",
            senderName,
            senderEmail,
            null,
            null,
            bodyPreview,
            true,
            false
        );

    private static EventDto CreateEvent(string? bodyPreview = "Preview") =>
        new(
            BridgeIdCodec.EventId("gid", "entry-2", FixedTimestamp),
            "gid",
            "Subject",
            FixedTimestamp,
            FixedTimestamp.AddHours(1),
            "Room 1",
            null,
            null,
            false,
            null,
            "Organizer",
            null,
            null,
            null,
            bodyPreview,
            true,
            false
        );
}
