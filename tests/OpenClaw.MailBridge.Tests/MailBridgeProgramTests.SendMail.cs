using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge.Contracts.Models;
using ClientProgram = OpenClaw.MailBridge.Client.Program;

namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Send-mail command-build tests for <see cref="ClientProgram"/>, split out of
/// <see cref="MailBridgeProgramTests"/> to keep each source file within the
/// 500-line limit. Behavior is unchanged; the test methods are moved verbatim.
/// </summary>
public partial class MailBridgeProgramTests
{
    // ─── Build: send-mail ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the <c>send-mail</c> arm maps lower-snaked option keys (as produced by the option
    /// parser) to the hyphenated <c>send_mail</c> RPC param keys, forwards recipient JSON verbatim
    /// (D-C), and sets the method to <c>send_mail</c>.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsSendMail_WithRequiredOptions_ReturnsSendMailRequest()
    {
        // Arrange (keys as the option parser lower-snakes them)
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["subject"] = "Hello",
            ["body_content_type"] = "HTML",
            ["body_content"] = "<p>hi</p>",
            ["to_recipients"] = """[{"emailAddress":{"address":"to@b.c"}}]""",
            ["cc_recipients"] = """[{"emailAddress":{"address":"cc@b.c"}}]""",
            ["bcc_recipients"] = """[{"emailAddress":{"address":"bcc@b.c"}}]""",
            ["save_to_sent_items"] = "false",
        };

        // Act
        var req = ClientProgram.Build("send-mail", opts);

        // Assert
        req.Should().NotBeNull();
        req!.Method.Should().Be(BridgeMethods.SendMail);
        req.Params.Should().NotBeNull();
        req.Params!["subject"].Should().Be("Hello");
        req.Params["body-content-type"].Should().Be("HTML");
        req.Params["body-content"].Should().Be("<p>hi</p>");
        req.Params["to-recipients"].Should().Be("""[{"emailAddress":{"address":"to@b.c"}}]""");
        req.Params["cc-recipients"].Should().Be("""[{"emailAddress":{"address":"cc@b.c"}}]""");
        req.Params["bcc-recipients"].Should().Be("""[{"emailAddress":{"address":"bcc@b.c"}}]""");
        req.Params["save-to-sent-items"].Should().Be("false");
    }

    /// <summary>
    /// Verifies <c>send-mail</c> with a missing required option returns null.
    /// </summary>
    [TestMethod]
    public void Build_WhenCommandIsSendMail_WithMissingToRecipients_ReturnsNull()
    {
        // Arrange: to-recipients missing
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["body_content_type"] = "Text",
        };

        // Act
        var req = ClientProgram.Build("send-mail", opts);

        // Assert
        req.Should().BeNull();
    }
}
