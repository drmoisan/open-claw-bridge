using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.MailBridge;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge.Tests;

public partial class MailBridgeRuntimeTests
{
    private static PipeRpcWorker SendMailWorker(FakeOutlookMailSender sender) =>
        new(
            BridgeSettings.Default,
            new BridgeStateStore(BridgeSettings.Default),
            new FakeScanStateRepository(),
            NullLogger<PipeRpcWorker>.Instance,
            sender
        );

    private static RpcRequest SendMailRequest(Dictionary<string, string> overrides)
    {
        var p = new Dictionary<string, string>
        {
            ["subject"] = "Hello",
            ["body-content-type"] = "Text",
            ["body-content"] = "Body",
            ["to-recipients"] = """[{"emailAddress":{"address":"to@b.c"}}]""",
        };
        foreach (var kv in overrides)
        {
            p[kv.Key] = kv.Value;
        }

        return new RpcRequest("1", BridgeMethods.SendMail, p);
    }

    [TestMethod]
    public async Task Send_mail_valid_params_should_dispatch_and_return_success()
    {
        // Arrange
        var sender = new FakeOutlookMailSender();
        var worker = SendMailWorker(sender);

        // Act
        var response = await worker.Handle(
            SendMailRequest(
                new()
                {
                    ["cc-recipients"] = """[{"emailAddress":{"address":"cc@b.c"}}]""",
                    ["bcc-recipients"] = """[{"emailAddress":{"address":"bcc@b.c"}}]""",
                    ["save-to-sent-items"] = "false",
                }
            )
        );

        // Assert
        response.Ok.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Result.Should().BeNull();
        sender.SendCalls.Should().Be(1);
        sender.Received.Should().NotBeNull();
        sender.Received!.Subject.Should().Be("Hello");
        sender.Received.BodyContentType.Should().Be("Text");
        sender.Received.To.Should().ContainSingle().Which.Should().Be("to@b.c");
        sender.Received.Cc.Should().ContainSingle().Which.Should().Be("cc@b.c");
        sender.Received.Bcc.Should().ContainSingle().Which.Should().Be("bcc@b.c");
        sender.Received.SaveToSentItems.Should().BeFalse();
    }

    [TestMethod]
    public async Task Send_mail_sender_throwing_should_map_to_internal_error()
    {
        // Arrange
        var sender = new FakeOutlookMailSender { ThrowOnSend = true };
        var worker = SendMailWorker(sender);

        // Act
        var response = await worker.Handle(SendMailRequest(new()));

        // Assert
        response.Ok.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InternalError);
    }

    [TestMethod]
    public async Task Send_mail_no_recipients_should_return_invalid_request()
    {
        // Arrange
        var sender = new FakeOutlookMailSender();
        var worker = SendMailWorker(sender);
        var req = SendMailRequest(new() { ["to-recipients"] = "[]" });

        // Act
        var response = await worker.Handle(req);

        // Assert
        response.Ok.Should().BeFalse();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        sender.SendCalls.Should().Be(0);
    }

    [TestMethod]
    public async Task Send_mail_invalid_content_type_should_return_invalid_request()
    {
        // Arrange
        var sender = new FakeOutlookMailSender();
        var worker = SendMailWorker(sender);
        var req = SendMailRequest(new() { ["body-content-type"] = "Markdown" });

        // Act
        var response = await worker.Handle(req);

        // Assert
        response.Ok.Should().BeFalse();
        response.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        sender.SendCalls.Should().Be(0);
    }

    [TestMethod]
    public async Task Send_mail_empty_subject_should_be_accepted()
    {
        // Arrange
        var sender = new FakeOutlookMailSender();
        var worker = SendMailWorker(sender);
        var req = SendMailRequest(new() { ["subject"] = string.Empty });

        // Act
        var response = await worker.Handle(req);

        // Assert
        response.Ok.Should().BeTrue();
        sender.Received!.Subject.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Send_mail_save_to_sent_items_should_default_to_true_when_absent()
    {
        // Arrange
        var sender = new FakeOutlookMailSender();
        var worker = SendMailWorker(sender);

        // Act (no save-to-sent-items key)
        var response = await worker.Handle(SendMailRequest(new()));

        // Assert
        response.Ok.Should().BeTrue();
        sender.Received!.SaveToSentItems.Should().BeTrue();
    }
}
