using FluentAssertions;
using NUnit.Framework;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

public class MailBridgeTests
{
    [Test]
    public void Bridge_id_codec_should_follow_spec_prefixes()
    {
        BridgeIdCodec.MessageId("abc", false).Should().StartWith("msg:");
        BridgeIdCodec.MessageId("abc", true).Should().StartWith("mtg:");
        BridgeIdCodec.EventId("gid", "eid", DateTimeOffset.Parse("2026-01-01T00:00:00Z")).Should().StartWith("evt:");
    }

    [Test]
    public void Settings_validator_rejects_invalid_mode()
    {
        var s = BridgeSettings.Default with { Mode = "bad" };
        BridgeSettingsValidator.Validate(s).Should().Contain(x => x.Contains("mode"));
    }

    [Test]
    public void Body_sanitizer_removes_html_and_paths()
    {
        var input = "<b>Hello</b> C:\\secret\\file.txt";
        var output = BodySanitizer.NormalizePreview(input, 500);
        output.Should().Contain("Hello");
        output.Should().NotContain("C:\\secret");
    }
}
