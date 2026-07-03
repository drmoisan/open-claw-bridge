using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Handler-level request-shape tests for interface members 2-4 (list messages, get
/// message, list meeting requests): exact URL/query composition with the spec
/// <c>$select</c> list pinned (including <c>meetingMessageType</c>, the shipped D10
/// primary form), GET method, bearer/client-request-id/Prefer headers, multi-page
/// <c>@odata.nextLink</c> accumulation, truncation at <c>limit</c>, the
/// <c>MaxPages</c> bound with its warning log, and the D10 client-side
/// <c>eventMessage</c> filter.
/// </summary>
[TestClass]
public sealed class GraphHostAdapterClientMessagesTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The spec message field list, pinned as a literal (D4/D10 primary form).</summary>
    private const string ExpectedMessageSelect =
        "id,subject,bodyPreview,receivedDateTime,sentDateTime,importance,sensitivity,"
        + "isRead,hasAttachments,conversationId,from,sender,toRecipients,ccRecipients,"
        + "meetingMessageType";

    private static GraphHostAdapterClient Client(
        FakeHttpHandler handler,
        GraphAdapterOptions? options = null,
        ILogger<GraphHostAdapterClient>? logger = null
    )
    {
        var tokenProvider = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok-msgs", Start.AddHours(1)));

        return new GraphHostAdapterClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            Options.Create(
                options
                    ?? new GraphAdapterOptions
                    {
                        Enabled = true,
                        PrincipalMailboxUpn = "paula@contoso.com",
                        AssistantMailboxUpn = "amy@contoso.com",
                    }
            ),
            tokenProvider.Object,
            new FakeTimeProvider(Start),
            logger ?? NullLogger<GraphHostAdapterClient>.Instance
        );
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static string SinglePage(params string[] items) =>
        "{ \"value\": [" + string.Join(",", items) + "] }";

    private static string PageWithNext(string nextLink, params string[] items) =>
        "{ \"value\": ["
        + string.Join(",", items)
        + "], \"@odata.nextLink\": \""
        + nextLink
        + "\" }";

    [TestMethod]
    public async Task ListMessages_ComposesTheExactRequestShape()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json(GraphPayloadFixtures.MessageListPage2));
        });
        var client = Client(handler);

        var result = await client.ListMessagesAsync(Since, requestId: "req-list");

        result.Ok.Should().BeTrue();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri!.AbsolutePath.Should().Be("/v1.0/users/paula%40contoso.com/messages");
        Uri.UnescapeDataString(captured.RequestUri.Query)
            .Should()
            .Be(
                "?$filter=receivedDateTime ge 2026-07-01T00:00:00.0000000+00:00"
                    + "&$orderby=receivedDateTime desc"
                    + "&$top=50"
                    + $"&$select={ExpectedMessageSelect}",
                "the query pins the filter, ordering, min(limit, PageSize) top, and spec $select"
            );
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-msgs");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-list");
        captured
            .Headers.GetValues("Prefer")
            .Should()
            .BeEquivalentTo("outlook.timezone=\"UTC\"", "outlook.body-content-type=\"text\"");
    }

    [TestMethod]
    public async Task ListMessages_FollowsNextLinkAndAccumulatesInOrder()
    {
        var requestUris = new List<string>();
        var page = 0;
        var handler = new FakeHttpHandler(request =>
        {
            requestUris.Add(request.RequestUri!.AbsoluteUri);
            page++;
            return Task.FromResult(
                Json(
                    page == 1
                        ? GraphPayloadFixtures.MessageListPage1
                        : GraphPayloadFixtures.MessageListPage2
                )
            );
        });
        var client = Client(handler);

        var result = await client.ListMessagesAsync(Since, requestId: "req-pages");

        result.Ok.Should().BeTrue();
        requestUris.Should().HaveCount(2);
        requestUris[1]
            .Should()
            .Be(
                "https://graph.example.test/v1.0/users/p%40contoso.com/messages?$skip=2",
                "the second request follows the @odata.nextLink verbatim"
            );
        result
            .Data!.Items.Select(m => m.BridgeId)
            .Should()
            .Equal(
                new[] { "msg-001", "msg-mtg-001", "msg-sparse-001" },
                "pages accumulate in order"
            );
    }

    [TestMethod]
    public async Task ListMessages_TruncatesAtLimitAndStopsPaging()
    {
        var calls = 0;
        var handler = new FakeHttpHandler(request =>
        {
            calls++;
            return Task.FromResult(Json(GraphPayloadFixtures.MessageListPage1));
        });
        var client = Client(handler);

        var result = await client.ListMessagesAsync(Since, limit: 1, requestId: "req-limit");

        result.Ok.Should().BeTrue();
        calls.Should().Be(1, "the limit is satisfied by the first page");
        result.Data!.Items.Should().ContainSingle().Which.BridgeId.Should().Be("msg-001");
    }

    [TestMethod]
    public async Task ListMessages_TopIsMinOfLimitAndPageSize()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json(GraphPayloadFixtures.MessageListPage2));
        });
        var client = Client(handler);

        await client.ListMessagesAsync(Since, limit: 7, requestId: "req-top");

        Uri.UnescapeDataString(captured!.RequestUri!.Query)
            .Should()
            .Contain("&$top=7&", "a limit below PageSize drives $top");
    }

    [TestMethod]
    public async Task ListMessages_MaxPagesBound_TruncatesWithWarning()
    {
        var logger = new CapturingLogger();
        var page = 0;
        var handler = new FakeHttpHandler(_ =>
        {
            page++;
            return Task.FromResult(
                Json(
                    PageWithNext(
                        $"https://graph.example.test/v1.0/users/p/messages?$skip={page}",
                        GraphPayloadFixtures.MessageSparse
                    )
                )
            );
        });
        var options = new GraphAdapterOptions
        {
            Enabled = true,
            PrincipalMailboxUpn = "paula@contoso.com",
            AssistantMailboxUpn = "amy@contoso.com",
            MaxPages = 2,
        };
        var client = Client(handler, options, logger);

        var result = await client.ListMessagesAsync(Since, limit: 10, requestId: "req-max");

        result.Ok.Should().BeTrue("hitting MaxPages returns the truncated set as a success");
        page.Should().Be(2, "paging stops at the MaxPages bound");
        result.Data!.Items.Should().HaveCount(2);
        logger
            .Levels.Should()
            .ContainSingle("truncation by MaxPages is logged exactly once")
            .Which.Should()
            .Be(LogLevel.Warning);
    }

    /// <summary>
    /// A minimal capturing logger (Moq cannot proxy <c>ILogger&lt;T&gt;</c> closed
    /// over an internal type without granting InternalsVisibleTo to DynamicProxy).
    /// </summary>
    private sealed class CapturingLogger : ILogger<GraphHostAdapterClient>
    {
        public List<LogLevel> Levels { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Levels.Add(logLevel);
    }

    [TestMethod]
    public async Task GetMessage_ComposesTheExactRequestShapeWithEscapedId()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json(GraphPayloadFixtures.MessageFull));
        });
        var client = Client(handler);

        var result = await client.GetMessageAsync("AAMk+abc=", requestId: "req-get");

        result.Ok.Should().BeTrue();
        result.Data!.BridgeId.Should().Be("msg-001");
        captured!.Method.Should().Be(HttpMethod.Get);
        captured
            .RequestUri!.AbsolutePath.Should()
            .Be(
                "/v1.0/users/paula%40contoso.com/messages/AAMk%2Babc%3D",
                "the Graph message id is URL-escaped into the route segment"
            );
        Uri.UnescapeDataString(captured.RequestUri.Query)
            .Should()
            .Be($"?$select={ExpectedMessageSelect}");
        captured.Headers.Authorization!.ToString().Should().Be("Bearer tok-msgs");
        captured.Headers.GetValues("client-request-id").Should().Equal("req-get");
        captured
            .Headers.GetValues("Prefer")
            .Should()
            .BeEquivalentTo("outlook.timezone=\"UTC\"", "outlook.body-content-type=\"text\"");
    }

    [TestMethod]
    public async Task ListMeetingRequests_FiltersToEventMessagesClientSide()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(request =>
        {
            captured = request;
            return Task.FromResult(
                Json(
                    SinglePage(
                        GraphPayloadFixtures.MessageFull,
                        GraphPayloadFixtures.MeetingRequestMessage
                    )
                )
            );
        });
        var client = Client(handler);

        var result = await client.ListMeetingRequestsAsync(Since, requestId: "req-mtg");

        result.Ok.Should().BeTrue();
        result
            .Data!.Items.Should()
            .ContainSingle("the mixed page yields only the eventMessage item")
            .Which.BridgeId.Should()
            .Be("msg-mtg-001");
        result.Data.Items[0].ItemKind.Should().Be("meeting");
        // D10 pin: the shipped primary form keeps meetingMessageType in $select on the
        // base /messages collection.
        Uri.UnescapeDataString(captured!.RequestUri!.Query)
            .Should()
            .Contain($"$select={ExpectedMessageSelect}")
            .And.Contain("meetingMessageType");
    }

    [TestMethod]
    public async Task ListMeetingRequests_PagesUntilLimitMeetingMessagesFound()
    {
        var page = 0;
        var handler = new FakeHttpHandler(_ =>
        {
            page++;
            return Task.FromResult(
                Json(
                    page == 1
                        ? PageWithNext(
                            "https://graph.example.test/v1.0/users/p/messages?$skip=2",
                            GraphPayloadFixtures.MessageFull,
                            GraphPayloadFixtures.MeetingRequestMessage
                        )
                        : SinglePage(GraphPayloadFixtures.MeetingRequestMessage)
                )
            );
        });
        var client = Client(handler);

        var result = await client.ListMeetingRequestsAsync(Since, limit: 2, requestId: "req-m2");

        result.Ok.Should().BeTrue();
        page.Should().Be(2, "one meeting message per page requires a second page for limit 2");
        result.Data!.Items.Should().HaveCount(2);
        result.Data.Items.Should().OnlyContain(m => m.ItemKind == "meeting");
    }
}
