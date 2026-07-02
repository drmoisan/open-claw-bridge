using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Composition-invariant tests for issues #18 x #20 (spec Group C): normalization-time
/// sensitivity redaction must survive shaping in both modes, the shapers must never mutate
/// <c>IsRedacted</c>, and <c>ProtectedFieldsAvailable = false</c> must hold on both paths.
/// Redacted inputs are built with the Phase 1 transforms so the tested composition is exactly
/// the production write-then-read pipeline.
/// </summary>
[TestClass]
public sealed class ResponseShaperCompositionInvariantTests
{
    private static readonly DateTimeOffset FixedReceived = DateTimeOffset.Parse(
        "2026-07-01T09:00:00Z"
    );
    private static readonly DateTimeOffset FixedStart = DateTimeOffset.Parse(
        "2026-07-03T14:00:00Z"
    );

    private static BridgeSettings SafeSettings => BridgeSettings.Default with { Mode = "safe" };

    private static BridgeSettings EnhancedSettings =>
        BridgeSettings.Default with
        {
            Mode = "enhanced",
        };

    private static MessageDto CreateMessage(bool isRedacted = false) =>
        new(
            BridgeIdCodec.MessageId("entry-compose-1", false),
            "mail",
            "Subject",
            FixedReceived,
            null,
            null,
            2,
            false,
            false,
            "IPM.Note",
            "Sender",
            "sender@example.com",
            "[{\"name\":\"To\",\"email\":\"to@example.com\"}]",
            "[{\"name\":\"Cc\",\"email\":\"cc@example.com\"}]",
            "Preview",
            true,
            isRedacted,
            SenderEmailResolved: "resolved@example.com",
            FromEmailAddress: "from@example.com"
        );

    private static EventDto CreateEvent(bool isRedacted = false) =>
        new(
            BridgeIdCodec.EventId("gid-compose", "entry-compose-2", FixedStart),
            "gid-compose",
            "Subject",
            FixedStart,
            FixedStart.AddHours(1),
            "Room 1",
            2,
            1,
            false,
            3,
            "Organizer",
            "[{\"name\":\"Req\",\"email\":\"req@example.com\"}]",
            null,
            null,
            "Preview",
            true,
            isRedacted,
            Categories: new[] { "Cat" },
            BodyFull: "Body",
            SensitivityLabel: "confidential"
        );

    private static MessageDto RedactedMessage() => OutlookScanner.RedactMessage(CreateMessage());

    private static EventDto RedactedEvent() => OutlookScanner.RedactEvent(CreateEvent());

    [TestMethod]
    public void Redacted_message_should_survive_enhanced_mode_shaping()
    {
        // C1: cache-written redaction survives enhanced shaping; IsRedacted stays true.
        var shaped = ResponseShaper.ShapeMessage(RedactedMessage(), EnhancedSettings);

        shaped.IsRedacted.Should().BeTrue("enhanced shaping must not falsify the redaction flag");
        shaped.Subject.Should().Be("Private message");
        shaped.SenderName.Should().BeNull();
        shaped.SenderEmail.Should().BeNull();
        shaped.SenderEmailResolved.Should().BeNull();
        shaped.FromEmailAddress.Should().BeNull();
        shaped.ToJson.Should().BeNull();
        shaped.CcJson.Should().BeNull();
        shaped.BodyPreview.Should().BeNull();
        shaped.ProtectedFieldsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void Redacted_event_should_survive_enhanced_mode_shaping()
    {
        // C1: the event-path equivalent, including the empty-categories invariant.
        var shaped = ResponseShaper.ShapeEvent(RedactedEvent(), EnhancedSettings);

        shaped.IsRedacted.Should().BeTrue("enhanced shaping must not falsify the redaction flag");
        shaped.Subject.Should().Be("Private appointment");
        shaped.Location.Should().BeNull();
        shaped.Organizer.Should().BeNull();
        shaped.RequiredAttendeesJson.Should().BeNull();
        shaped.BodyPreview.Should().BeNull();
        shaped.BodyFull.Should().BeNull();
        shaped.Categories.Should().NotBeNull();
        shaped.Categories.Should().BeEmpty();
        shaped.ProtectedFieldsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void Redacted_dtos_should_keep_is_redacted_through_safe_mode_without_error()
    {
        // C2: re-nulling already-null fields is a no-op and must not throw.
        var shapeMessage = () => ResponseShaper.ShapeMessage(RedactedMessage(), SafeSettings);
        var shapeEvent = () => ResponseShaper.ShapeEvent(RedactedEvent(), SafeSettings);

        shapeMessage.Should().NotThrow();
        shapeEvent.Should().NotThrow();
        shapeMessage().IsRedacted.Should().BeTrue();
        shapeEvent().IsRedacted.Should().BeTrue();
    }

    [TestMethod]
    public void Shapers_should_never_mutate_is_redacted_in_either_mode()
    {
        // C3: safe mode with input false stays false; enhanced mode with input true stays true.
        ResponseShaper
            .ShapeMessage(CreateMessage(isRedacted: false), SafeSettings)
            .IsRedacted.Should()
            .BeFalse("safe mode must never set IsRedacted");
        ResponseShaper
            .ShapeEvent(CreateEvent(isRedacted: false), SafeSettings)
            .IsRedacted.Should()
            .BeFalse("safe mode must never set IsRedacted");
        ResponseShaper
            .ShapeMessage(CreateMessage(isRedacted: true), EnhancedSettings)
            .IsRedacted.Should()
            .BeTrue("enhanced mode must never reset IsRedacted");
        ResponseShaper
            .ShapeEvent(CreateEvent(isRedacted: true), EnhancedSettings)
            .IsRedacted.Should()
            .BeTrue("enhanced mode must never reset IsRedacted");
    }

    [TestMethod]
    public void Protected_fields_available_false_should_hold_on_both_paths()
    {
        // C4: redaction-written false survives enhanced shaping; safe mode forces false on
        // unredacted DTOs.
        ResponseShaper
            .ShapeMessage(RedactedMessage(), EnhancedSettings)
            .ProtectedFieldsAvailable.Should()
            .BeFalse("redaction-written value must survive enhanced shaping");
        ResponseShaper
            .ShapeEvent(RedactedEvent(), EnhancedSettings)
            .ProtectedFieldsAvailable.Should()
            .BeFalse("redaction-written value must survive enhanced shaping");
        ResponseShaper
            .ShapeMessage(CreateMessage(), SafeSettings)
            .ProtectedFieldsAvailable.Should()
            .BeFalse("safe mode must force the flag on unredacted DTOs");
        ResponseShaper
            .ShapeEvent(CreateEvent(), SafeSettings)
            .ProtectedFieldsAvailable.Should()
            .BeFalse("safe mode must force the flag on unredacted DTOs");
    }
}
