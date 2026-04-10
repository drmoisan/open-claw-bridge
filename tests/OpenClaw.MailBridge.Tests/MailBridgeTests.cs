using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

[TestClass]
public class MailBridgeTests
{
    [TestMethod]
    public void Bridge_id_codec_should_follow_spec_prefixes()
    {
        BridgeIdCodec.MessageId("abc", false).Should().StartWith("msg:");
        BridgeIdCodec.MessageId("abc", true).Should().StartWith("mtg:");
        BridgeIdCodec
            .EventId("gid", "eid", DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
            .Should()
            .StartWith("evt:");
    }

    [TestMethod]
    public void Settings_validator_rejects_invalid_mode()
    {
        var s = BridgeSettings.Default with { Mode = "bad" };
        BridgeSettingsValidator.Validate(s).Should().Contain(x => x.Contains("mode"));
    }

    [TestMethod]
    public void Body_sanitizer_removes_html_and_paths()
    {
        var input = "<b>Hello</b> C:\\secret\\file.txt";
        var output = BodySanitizer.NormalizePreview(input, 500);
        output.Should().Contain("Hello");
        output.Should().NotContain("C:\\secret");
    }

    [TestMethod]
    public void Safe_mode_message_shaping_should_suppress_body_preview_sender_name_and_sender_email()
    {
        var shaped = ResponseShaper.ShapeMessage(
            new MessageDto(
                BridgeIdCodec.MessageId("entry-1", false),
                "mail",
                "Subject",
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                false,
                false,
                "IPM.Note",
                "Sender",
                "sender@example.com",
                null,
                null,
                "Preview",
                true,
                false
            ),
            BridgeSettings.Default with
            {
                Mode = "safe",
            }
        );

        shaped.BodyPreview.Should().BeNull();
        shaped.SenderName.Should().BeNull();
        shaped.SenderEmail.Should().BeNull();
    }

    [TestMethod]
    public void Enhanced_mode_message_shaping_should_use_sanitized_and_truncated_preview_text()
    {
        var shaped = ResponseShaper.ShapeMessage(
            new MessageDto(
                BridgeIdCodec.MessageId("entry-2", false),
                "mail",
                "Subject",
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                false,
                false,
                "IPM.Note",
                "Sender",
                "sender@example.com",
                null,
                null,
                "<b>Hello</b> C:\\secret\\file.txt and a very long trailer",
                true,
                false
            ),
            BridgeSettings.Default with
            {
                Mode = "enhanced",
                BodyPreviewMaxChars = 12,
            }
        );

        shaped.BodyPreview.Should().Be("Hello [path]");
    }
}
