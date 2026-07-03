using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Handler-level tests for member 9 (<c>SendMailAsync</c>): the POST goes to the
/// assistant mailbox <c>{a}</c>; the body carries <c>from</c> = principal when
/// <c>{p} != {a}</c> and omits <c>from</c> when they match (structural JSON
/// assertions matching the spec API example); <c>saveToSentItems</c> passes through;
/// 202 yields <c>ok: true, data: null</c>; and endpoint-level D5 error-mapping
/// samples (400, 401, 429 exhaustion with <c>TooManyRequests</c> passthrough).
/// </summary>
[TestClass]
public sealed class GraphHostAdapterClientSendMailTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

    private static GraphHostAdapterClient Client(
        FakeHttpHandler handler,
        string principal = "paula@contoso.com",
        string assistant = "amy@contoso.com",
        FakeTimeProvider? timeProvider = null,
        int maxAttempts = 4
    )
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-send", Start.AddHours(1)));

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(
                new GraphAdapterOptions
                {
                    Enabled = true,
                    PrincipalMailboxUpn = principal,
                    AssistantMailboxUpn = assistant,
                    MaxAttempts = maxAttempts,
                }
            ),
            tokenProvider.Object,
            timeProvider ?? new FakeTimeProvider(Start),
            NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    private static SendMailRequest Request(bool saveToSentItems = true) =>
        new(
            new SendMailMessageDto(
                Subject: "Re: scheduling",
                Body: new SendMailBodyDto("Text", "Proposed times below."),
                ToRecipients:
                [
                    new SendMailRecipientDto(
                        new SendMailEmailAddressDto("rex@example.com", "Rex R")
                    ),
                ]
            ),
            SaveToSentItems: saveToSentItems
        );

    private static HttpResponseMessage Accepted() => new(HttpStatusCode.Accepted);

    [TestMethod]
    public async Task SendMail_PostsToTheAssistantMailbox()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Accepted());
        });
        var client = Client(handler);

        var result = await client.SendMailAsync(Request(), requestId: "req-send");

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be(
                "/v1.0/users/amy%40contoso.com/sendMail",
                "sendMail submits through the assistant mailbox"
            );
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-send");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-send");
    }

    [TestMethod]
    public async Task SendMail_PrincipalDiffersFromAssistant_InjectsFrom()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Accepted();
        });
        var client = Client(handler);

        await client.SendMailAsync(Request(), requestId: "req-from");

        using var body = JsonDocument.Parse(capturedBody!);
        var message = body.RootElement.GetProperty("message");
        message
            .GetProperty("from")
            .GetProperty("emailAddress")
            .GetProperty("address")
            .GetString()
            .Should()
            .Be("paula@contoso.com", "from is the principal when principal != assistant");
        message.GetProperty("subject").GetString().Should().Be("Re: scheduling");
        message.GetProperty("body").GetProperty("contentType").GetString().Should().Be("Text");
        message
            .GetProperty("body")
            .GetProperty("content")
            .GetString()
            .Should()
            .Be("Proposed times below.");
        message
            .GetProperty("toRecipients")[0]
            .GetProperty("emailAddress")
            .GetProperty("address")
            .GetString()
            .Should()
            .Be("rex@example.com");
        body.RootElement.GetProperty("saveToSentItems").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task SendMail_PrincipalEqualsAssistant_OmitsFrom()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Accepted();
        });
        var client = Client(
            handler,
            principal: "paula@contoso.com",
            assistant: "paula@contoso.com"
        );

        await client.SendMailAsync(Request(), requestId: "req-self");

        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("message")
            .TryGetProperty("from", out _)
            .Should()
            .BeFalse("no from injection when the principal sends through its own mailbox");
    }

    [TestMethod]
    public async Task SendMail_SaveToSentItemsFalse_PassesThrough()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Accepted();
        });
        var client = Client(handler);

        await client.SendMailAsync(Request(saveToSentItems: false), requestId: "req-sts");

        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("saveToSentItems").GetBoolean().Should().BeFalse();
    }

    [TestMethod]
    public async Task SendMail_Accepted202EmptyBody_YieldsOkTrueDataNull()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(Accepted()));
        var client = Client(handler);

        var result = await client.SendMailAsync(Request(), requestId: "req-202");

        result.Ok.Should().BeTrue("202 Accepted is the success contract (D-A)");
        result.Data.Should().BeNull();
        result.Error.Should().BeNull();
        result.Meta.RequestId.Should().Be("req-202");
        result.Meta.AdapterVersion.Should().Be("cloudgraph");
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.BadRequest, "INVALID_REQUEST", DisplayName = "400 -> INVALID_REQUEST")]
    [DataRow(HttpStatusCode.Unauthorized, "UNAUTHORIZED", DisplayName = "401 -> UNAUTHORIZED")]
    public async Task SendMail_TerminalStatus_MapsPerTheD5Matrix(
        HttpStatusCode status,
        string expectedCode
    )
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") })
        );
        var client = Client(handler);

        var result = await client.SendMailAsync(Request(), requestId: "req-err");

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        result.Error.Retryable.Should().BeFalse();
    }

    [TestMethod]
    public async Task SendMail_ThrottledExhaustion_MapsToThrottledWithGraphCodePassthrough()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attempts = 0;
        var handler = new FakeHttpHandler(_ =>
        {
            attempts++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(GraphPayloadFixtures.TooManyRequestsBody),
                }
            );
        });
        var client = Client(handler, timeProvider: timeProvider, maxAttempts: 2);

        var task = client.SendMailAsync(Request(), requestId: "req-429");
        var safety = 0;
        while (!task.IsCompleted)
        {
            if (++safety > 10_000)
            {
                throw new AssertFailedException("The send did not complete under fake time.");
            }

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        var result = await task;

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("THROTTLED");
        result.Error.Retryable.Should().BeTrue();
        result
            .Error.BridgeErrorCode.Should()
            .Be("TooManyRequests", "the Graph error.code passes through");
        attempts.Should().Be(2);
    }
}
