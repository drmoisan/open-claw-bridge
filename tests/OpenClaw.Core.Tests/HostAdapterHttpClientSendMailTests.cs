using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for <see cref="HostAdapterHttpClient.SendMailAsync"/>. Uses the shared
/// <see cref="FakeHttpHandler"/> to intercept outbound HTTP and replaces the token seam to avoid
/// filesystem I/O. No real network.
/// </summary>
[TestClass]
public class HostAdapterHttpClientSendMailTests
{
    private static IOptions<OpenClawOptions> BuildOptions(
        string tokenFile = "/run/openclaw/hostadapter.token"
    )
    {
        var opts = new OpenClawOptions();
        opts.HostAdapter.TokenFile = tokenFile;
        opts.HostAdapter.BaseUrl = "http://localhost:4319/";
        opts.HostAdapter.MailboxId = "me";
        return Options.Create(opts);
    }

    private static HostAdapterHttpClient BuildClient(
        FakeHttpHandler handler,
        Func<string, CancellationToken, Task<string?>> tokenReader
    )
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:4319/"),
        };
        return new HostAdapterHttpClient(httpClient, BuildOptions()) { TokenReader = tokenReader };
    }

    private static Func<string, CancellationToken, Task<string?>> ConstantTokenReader(
        string? token
    ) => (_, _) => Task.FromResult(token);

    private static SendMailRequest SampleRequest() =>
        new(
            new SendMailMessageDto(
                "Hello",
                new SendMailBodyDto("Text", "Body"),
                [new SendMailRecipientDto(new SendMailEmailAddressDto("to@b.c", "To Person"))]
            )
        );

    private static HttpResponseMessage AcceptedEnvelope(string requestId)
    {
        var envelope = new ApiEnvelope<object?>(
            true,
            null,
            new ApiMeta(requestId, "1.0", null),
            null
        );
        return new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(envelope),
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };
    }

    [TestMethod]
    public async Task SendMailAsync_should_POST_to_users_me_sendMail()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(AcceptedEnvelope("r1"));
        });
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        // Act
        await client.SendMailAsync(SampleRequest());

        // Assert
        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsolutePath.Should().Be("/users/me/sendMail");
    }

    [TestMethod]
    public async Task SendMailAsync_should_serialize_graph_shaped_body()
    {
        // Arrange
        string? body = null;
        var handler = new FakeHttpHandler(async req =>
        {
            body = await req.Content!.ReadAsStringAsync();
            return AcceptedEnvelope("r2");
        });
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        // Act
        await client.SendMailAsync(SampleRequest());

        // Assert
        body.Should().NotBeNull();
        using var doc = JsonDocument.Parse(body!);
        var message = doc.RootElement.GetProperty("message");
        message.GetProperty("subject").GetString().Should().Be("Hello");
        message.GetProperty("body").GetProperty("contentType").GetString().Should().Be("Text");
        message
            .GetProperty("toRecipients")[0]
            .GetProperty("emailAddress")
            .GetProperty("address")
            .GetString()
            .Should()
            .Be("to@b.c");
        doc.RootElement.GetProperty("saveToSentItems").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task SendMailAsync_should_map_202_to_ok_true_data_null()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => Task.FromResult(AcceptedEnvelope("r3")));
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        // Act
        var result = await client.SendMailAsync(SampleRequest());

        // Assert
        result.Ok.Should().BeTrue();
        result.Data.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [TestMethod]
    public async Task SendMailAsync_when_token_missing_should_return_configuration_error_without_http_call()
    {
        // Arrange: handler throws if invoked, asserting no outbound request is made
        var invoked = false;
        var handler = new FakeHttpHandler(_ =>
        {
            invoked = true;
            throw new InvalidOperationException("HTTP must not be called when token is absent.");
        });
        var client = BuildClient(handler, ConstantTokenReader(null));

        // Act
        var result = await client.SendMailAsync(SampleRequest());

        // Assert
        invoked.Should().BeFalse();
        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CONFIGURATION_ERROR");
    }
}
