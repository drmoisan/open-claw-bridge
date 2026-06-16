using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

[TestClass]
public class HostAdapterSendMailTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static BridgeStatusDto ReadyBridge() =>
        new(
            BridgeState.ready.ToString(),
            BridgeMode.safe.ToString(),
            true,
            false,
            null,
            null,
            null
        );

    private static BridgeStatusDto StartingBridge() =>
        new(
            BridgeState.starting.ToString(),
            BridgeMode.safe.ToString(),
            false,
            false,
            null,
            null,
            null
        );

    private static void EnqueueStatus(
        HostAdapterTestWebApplicationFactory factory,
        BridgeStatusDto bridge
    ) =>
        factory.ProcessRunner.EnqueueResponse(
            "status",
            HostAdapterResponses.Success(bridge, "status-request", "test-version", bridge)
        );

    private static SendMailRequest ValidRequest(
        string contentType = "Text",
        bool withRecipient = true
    ) =>
        new(
            new SendMailMessageDto(
                "Hello",
                new SendMailBodyDto(contentType, "Body"),
                withRecipient
                    ? [new SendMailRecipientDto(new SendMailEmailAddressDto("to@b.c"))]
                    : []
            )
        );

    private static async Task<ApiEnvelope<object?>> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiEnvelope<object?>>(json, WebOptions)!;
    }

    [TestMethod]
    public async Task SendMail_valid_request_should_return_202_with_empty_envelope()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        var bridge = ReadyBridge();
        EnqueueStatus(factory, bridge);
        factory.ProcessRunner.EnqueueResponse(
            "send-mail",
            HostAdapterResponses.AcceptedNoContent("send-request", "test-version", bridge)
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            "/users/me/sendMail",
            ValidRequest(),
            WebOptions
        );
        var envelope = await ReadEnvelopeAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        envelope.Ok.Should().BeTrue();
        envelope.Data.Should().BeNull();
        envelope.Error.Should().BeNull();
        factory.ProcessRunner.Invocations.Select(i => i.Verb).Should().Equal("status", "send-mail");
    }

    [TestMethod]
    public async Task SendMail_no_recipients_should_return_400_without_dispatch()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        EnqueueStatus(factory, ReadyBridge());
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            "/users/me/sendMail",
            ValidRequest(withRecipient: false),
            WebOptions
        );
        var envelope = await ReadEnvelopeAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        envelope.Ok.Should().BeFalse();
        envelope.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        factory.ProcessRunner.Invocations.Select(i => i.Verb).Should().NotContain("send-mail");
    }

    [TestMethod]
    public async Task SendMail_invalid_content_type_should_return_400_without_dispatch()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        EnqueueStatus(factory, ReadyBridge());
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            "/users/me/sendMail",
            ValidRequest(contentType: "Markdown"),
            WebOptions
        );
        var envelope = await ReadEnvelopeAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        envelope.Ok.Should().BeFalse();
        envelope.Error!.Code.Should().Be(BridgeErrorCodes.InvalidRequest);
        factory.ProcessRunner.Invocations.Select(i => i.Verb).Should().NotContain("send-mail");
    }

    [TestMethod]
    public async Task SendMail_when_bridge_not_ready_should_return_409_without_dispatch()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        EnqueueStatus(factory, StartingBridge());
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            "/users/me/sendMail",
            ValidRequest(),
            WebOptions
        );
        var envelope = await ReadEnvelopeAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        envelope.Ok.Should().BeFalse();
        envelope.Error!.Code.Should().Be("BRIDGE_NOT_READY");
        factory.ProcessRunner.Invocations.Select(i => i.Verb).Should().NotContain("send-mail");
    }

    [TestMethod]
    public async Task SendMail_when_runner_fails_should_return_502()
    {
        // Arrange
        using var factory = new HostAdapterTestWebApplicationFactory();
        var bridge = ReadyBridge();
        EnqueueStatus(factory, bridge);
        factory.ProcessRunner.EnqueueResponse(
            "send-mail",
            HostAdapterResponses.Failure<object?>(
                502,
                "send-request",
                "test-version",
                BridgeErrorCodes.InternalError,
                "Outlook send failed.",
                bridge,
                BridgeErrorCodes.InternalError,
                retryable: false
            )
        );
        using var client = factory.CreateAuthorizedClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            "/users/me/sendMail",
            ValidRequest(),
            WebOptions
        );
        var envelope = await ReadEnvelopeAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        envelope.Ok.Should().BeFalse();
        envelope.Error!.Code.Should().Be(BridgeErrorCodes.InternalError);
    }

    // ─── BuildSendMail argument-sequence assertion (P8-T6, AC-05) ───────────────────

    [TestMethod]
    public void BuildSendMail_should_produce_send_mail_verb_with_json_recipient_arrays()
    {
        // Arrange
        var options = new HostAdapterOptions { ClientExecutablePath = "client.exe" };
        var builder = new HostAdapterCommandBuilder(Options.Create(options));
        var request = new SendMailRequest(
            new SendMailMessageDto(
                "Subj",
                new SendMailBodyDto("HTML", "<p>x</p>"),
                [new SendMailRecipientDto(new SendMailEmailAddressDto("to@b.c", "To"))],
                CcRecipients: [new SendMailRecipientDto(new SendMailEmailAddressDto("cc@b.c"))],
                BccRecipients: [new SendMailRecipientDto(new SendMailEmailAddressDto("bcc@b.c"))]
            ),
            SaveToSentItems: false
        );

        // Act
        var startInfo = builder.BuildSendMail(request);
        var args = startInfo.ArgumentList.ToArray();

        // Assert
        args[0].Should().Be("send-mail");
        args.Should().ContainInOrder("--subject", "Subj");
        args.Should().ContainInOrder("--body-content-type", "HTML");
        args.Should().ContainInOrder("--body-content", "<p>x</p>");
        args.Should()
            .ContainInOrder(
                "--to-recipients",
                """[{"emailAddress":{"address":"to@b.c","name":"To"}}]"""
            );
        args.Should()
            .ContainInOrder(
                "--cc-recipients",
                """[{"emailAddress":{"address":"cc@b.c","name":null}}]"""
            );
        args.Should()
            .ContainInOrder(
                "--bcc-recipients",
                """[{"emailAddress":{"address":"bcc@b.c","name":null}}]"""
            );
        args.Should().ContainInOrder("--save-to-sent-items", "false");
    }

    [TestMethod]
    public void BuildSendMail_should_default_save_to_sent_items_true_and_empty_recipient_arrays()
    {
        // Arrange
        var options = new HostAdapterOptions { ClientExecutablePath = "client.exe" };
        var builder = new HostAdapterCommandBuilder(Options.Create(options));
        var request = new SendMailRequest(
            new SendMailMessageDto(
                string.Empty,
                new SendMailBodyDto("Text", "x"),
                [new SendMailRecipientDto(new SendMailEmailAddressDto("to@b.c"))]
            )
        );

        // Act
        var args = builder.BuildSendMail(request).ArgumentList.ToArray();

        // Assert
        args.Should().ContainInOrder("--save-to-sent-items", "true");
        args.Should().ContainInOrder("--cc-recipients", "[]");
        args.Should().ContainInOrder("--bcc-recipients", "[]");
    }
}
