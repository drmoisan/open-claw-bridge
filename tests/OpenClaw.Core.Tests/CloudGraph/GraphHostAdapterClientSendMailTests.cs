using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
        int maxAttempts = 4,
        IEnumerable<string>? allowlist = null,
        ILogger<GraphHostAdapterClient>? logger = null
    )
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-send", Start.AddHours(1)));

        var options = new GraphAdapterOptions
        {
            Enabled = true,
            PrincipalMailboxUpn = principal,
            AssistantMailboxUpn = assistant,
            MaxAttempts = maxAttempts,
        };
        // Default: allowlist the configured principal so on-behalf sends are permitted.
        // Self-send tests (principal == assistant) are unaffected — self dominates.
        foreach (var entry in allowlist ?? new[] { principal })
        {
            options.AllowedPrincipalMailboxUpns.Add(entry);
        }

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(options),
            tokenProvider.Object,
            timeProvider ?? new FakeTimeProvider(Start),
            logger ?? NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    /// <summary>
    /// Builds a client whose <see cref="IAppTokenProvider"/> is a strict mock with no
    /// setups: any call throws, proving the deny path never acquires a token. The HTTP
    /// handler and token provider must both be untouched on a deny.
    /// </summary>
    private static GraphHostAdapterClient DenyClient(
        FakeHttpHandler handler,
        Mock<IAppTokenProvider> tokenProvider,
        string principal = "paula@contoso.com",
        string assistant = "amy@contoso.com",
        IEnumerable<string>? allowlist = null,
        ILogger<GraphHostAdapterClient>? logger = null
    )
    {
        var options = new GraphAdapterOptions
        {
            Enabled = true,
            PrincipalMailboxUpn = principal,
            AssistantMailboxUpn = assistant,
        };
        foreach (var entry in allowlist ?? Array.Empty<string>())
        {
            options.AllowedPrincipalMailboxUpns.Add(entry);
        }

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(options),
            tokenProvider.Object,
            new FakeTimeProvider(Start),
            logger ?? NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    /// <summary>
    /// A minimal capturing logger recording each entry's level and formatted message
    /// (Moq cannot proxy <c>ILogger&lt;T&gt;</c> over an internal type).
    /// </summary>
    private sealed class RecordingLogger : ILogger<GraphHostAdapterClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static FakeHttpHandler NeverInvokedHandler(Action onInvoke) =>
        new(request =>
        {
            onInvoke();
            return Task.FromResult(Accepted());
        });

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

    // ---- F15 send-on-behalf authorization gate (issue #119) ----

    [TestMethod]
    public async Task SendMail_NonAllowlistedPrincipal_DeniesBeforeAnyIo()
    {
        // (a) Decisive deny: {p} != {a}, principal absent from a non-empty allowlist.
        var handlerInvocations = 0;
        var handler = NeverInvokedHandler(() => handlerInvocations++);
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        var client = DenyClient(handler, tokenProvider, allowlist: ["someone-else@contoso.com"]);

        var result = await client.SendMailAsync(Request(), requestId: "req-deny-a");

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("UNAUTHORIZED");
        result.Error.BridgeErrorCode.Should().Be("SendOnBehalfDenied");
        result.Error.Retryable.Should().BeFalse();
        result.Meta.RequestId.Should().Be("req-deny-a");
        result.Meta.AdapterVersion.Should().Be("cloudgraph");
        result.Data.Should().BeNull();
        handlerInvocations.Should().Be(0, "the deny occurs before any HTTP request");
        tokenProvider.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "the deny occurs before token acquisition"
        );
    }

    [TestMethod]
    public async Task SendMail_EmptyAllowlist_DeniesBeforeAnyIo()
    {
        // (b) Empty/absent-allowlist default denies every on-behalf send.
        var handlerInvocations = 0;
        var handler = NeverInvokedHandler(() => handlerInvocations++);
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        var client = DenyClient(handler, tokenProvider);

        var result = await client.SendMailAsync(Request(), requestId: "req-deny-b");

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("UNAUTHORIZED");
        result.Error.BridgeErrorCode.Should().Be("SendOnBehalfDenied");
        result.Error.Retryable.Should().BeFalse();
        result.Meta.RequestId.Should().Be("req-deny-b");
        result.Meta.AdapterVersion.Should().Be("cloudgraph");
        handlerInvocations.Should().Be(0, "an empty allowlist denies before any HTTP request");
        tokenProvider.Verify(p => p.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SendMail_Denied_MessageNamesKeyAndLogsOneWarningWithRequestIdOnly()
    {
        // (c) The deny message names the key and echoes no UPN; exactly one warning
        // log carries the request id only.
        var handler = NeverInvokedHandler(() => { });
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        var recordingLogger = new RecordingLogger();
        var client = DenyClient(handler, tokenProvider, logger: recordingLogger);

        var result = await client.SendMailAsync(Request(), requestId: "req-deny-c");

        result
            .Error!.Message.Should()
            .Contain("OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns")
            .And.NotContain("paula@contoso.com")
            .And.NotContain("amy@contoso.com");

        var warnings = recordingLogger.Entries.FindAll(e => e.Level == LogLevel.Warning);
        warnings.Should().ContainSingle("the deny path logs exactly one warning");
        warnings[0]
            .Message.Should()
            .Contain("req-deny-c")
            .And.NotContain("paula@contoso.com")
            .And.NotContain("amy@contoso.com");
    }

    [TestMethod]
    public async Task SendMail_AllowlistedPrincipal_InjectsFromAndSucceeds()
    {
        // (d) Allowlisted {p} != {a}: POST reaches users/{a}/sendMail with from = {p}.
        string? capturedBody = null;
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(async request =>
        {
            captured = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Accepted();
        });
        var client = Client(handler, allowlist: ["paula@contoso.com"]);

        var result = await client.SendMailAsync(Request(), requestId: "req-allow-d");

        result.Ok.Should().BeTrue();
        result.Data.Should().BeNull();
        captured!.RequestUri!.AbsolutePath.Should().Be("/v1.0/users/amy%40contoso.com/sendMail");
        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("message")
            .GetProperty("from")
            .GetProperty("emailAddress")
            .GetProperty("address")
            .GetString()
            .Should()
            .Be("paula@contoso.com", "the allowlisted principal is the from address");
    }

    [TestMethod]
    public async Task SendMail_CaseDifferingAllowlistEntry_PermitsTheSend()
    {
        // (e) An allowlist entry differing only by case permits the on-behalf send.
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Accepted();
        });
        var client = Client(handler, allowlist: ["PAULA@Contoso.COM"]);

        var result = await client.SendMailAsync(Request(), requestId: "req-allow-e");

        result.Ok.Should().BeTrue("a case-differing allowlist entry is a member");
        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("message")
            .GetProperty("from")
            .GetProperty("emailAddress")
            .GetProperty("address")
            .GetString()
            .Should()
            .Be("paula@contoso.com");
    }

    [TestMethod]
    public async Task SendMail_SelfSendEmptyAllowlist_SucceedsWithoutFrom()
    {
        // (f) Self-send ({p} == {a}) succeeds with an empty allowlist and no from.
        string? capturedBody = null;
        var handler = new FakeHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return Accepted();
        });
        var client = Client(
            handler,
            principal: "paula@contoso.com",
            assistant: "paula@contoso.com",
            allowlist: []
        );

        var result = await client.SendMailAsync(Request(), requestId: "req-self-f");

        result.Ok.Should().BeTrue("self-send is unaffected by an empty allowlist");
        using var body = JsonDocument.Parse(capturedBody!);
        body.RootElement.GetProperty("message")
            .TryGetProperty("from", out _)
            .Should()
            .BeFalse("self-send injects no from");
    }
}
