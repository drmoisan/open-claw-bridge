using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.HostAdapter.Contracts;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Unit tests for <see cref="HostAdapterHttpClient"/>.
/// All tests replace the <c>TokenReader</c> seam to avoid filesystem I/O and use a
/// <see cref="FakeHttpHandler"/> to intercept and control outbound HTTP calls.
/// </summary>
[TestClass]
public class HostAdapterHttpClientTests
{
    // ─── Arrange helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="IOptions{OpenClawOptions}"/> with the given token file path
    /// and a fixed local base URL.
    /// </summary>
    private static IOptions<OpenClawOptions> BuildOptions(
        string tokenFile = "/run/openclaw/hostadapter.token"
    )
    {
        var opts = new OpenClawOptions();
        opts.HostAdapter.TokenFile = tokenFile;
        opts.HostAdapter.BaseUrl = "http://localhost:4319/";
        return Options.Create(opts);
    }

    /// <summary>
    /// Constructs a <see cref="HostAdapterHttpClient"/> whose HTTP layer is intercepted by
    /// <paramref name="handler"/> and whose token acquisition is replaced by
    /// <paramref name="tokenReader"/>.
    /// </summary>
    private static HostAdapterHttpClient BuildClient(
        FakeHttpHandler handler,
        Func<string, CancellationToken, Task<string?>> tokenReader,
        string tokenFile = "/run/openclaw/hostadapter.token"
    )
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:4319/"),
        };
        return new HostAdapterHttpClient(httpClient, BuildOptions(tokenFile))
        {
            TokenReader = tokenReader,
        };
    }

    /// <summary>
    /// Returns a token reader that always resolves to <paramref name="token"/>, regardless
    /// of the configured path.
    /// </summary>
    private static Func<string, CancellationToken, Task<string?>> ConstantTokenReader(
        string? token
    ) => (_, _) => Task.FromResult(token);

    /// <summary>
    /// Builds an <see cref="HttpResponseMessage"/> whose body is the JSON-serialized form of
    /// <paramref name="payload"/>, with the given <paramref name="status"/>.
    /// </summary>
    private static HttpResponseMessage JsonResponse<T>(
        T payload,
        HttpStatusCode status = HttpStatusCode.OK
    )
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    // ─── Envelope factory helpers ─────────────────────────────────────────────────

    private static ApiEnvelope<BridgeStatusDto> MakeStatusEnvelope(string requestId) =>
        new(
            true,
            new BridgeStatusDto("ready", "safe", true, false, null, null, null),
            new ApiMeta(requestId, "1.0", null),
            null
        );

    private static ApiEnvelope<ItemsResponse<T>> MakeItemsEnvelope<T>(string requestId) =>
        new(true, new ItemsResponse<T>([]), new ApiMeta(requestId, "1.0", null), null);

    private static ApiEnvelope<MessageDto> MakeMessageEnvelope(string requestId) =>
        new(
            true,
            new MessageDto(
                "bridge-1",
                "mail",
                "Test Subject",
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                true,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false
            ),
            new ApiMeta(requestId, "1.0", null),
            null
        );

    private static ApiEnvelope<EventDto> MakeEventEnvelope(string requestId) =>
        new(
            true,
            new EventDto(
                "event-1",
                null,
                "Test Event",
                new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false
            ),
            new ApiMeta(requestId, "1.0", null),
            null
        );

    // ─── CONFIGURATION_ERROR: missing or blank token ──────────────────────────────

    /// <summary>
    /// Verifies that a null token (e.g., file absent) produces a CONFIGURATION_ERROR envelope
    /// and does not attempt an HTTP call.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenTokenReaderReturnsNull_ReturnsConfigurationError()
    {
        // Arrange: handler throws if invoked, confirming no HTTP call is made
        var handler = new FakeHttpHandler(_ =>
            throw new InvalidOperationException("HTTP must not be called when token is absent")
        );
        var client = BuildClient(handler, ConstantTokenReader(null));

        // Act
        var result = await client.GetStatusAsync();

        // Assert
        result.Ok.Should().BeFalse("a missing token must prevent a successful request");
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CONFIGURATION_ERROR");
    }

    /// <summary>
    /// Verifies that an empty token string produces a CONFIGURATION_ERROR envelope.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenTokenReaderReturnsEmpty_ReturnsConfigurationError()
    {
        var handler = new FakeHttpHandler(_ =>
            throw new InvalidOperationException("HTTP must not be called when token is empty")
        );
        var client = BuildClient(handler, ConstantTokenReader(string.Empty));

        var result = await client.GetStatusAsync();

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("CONFIGURATION_ERROR");
    }

    /// <summary>
    /// Verifies that a whitespace-only token string produces a CONFIGURATION_ERROR envelope.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenTokenReaderReturnsWhitespace_ReturnsConfigurationError()
    {
        var handler = new FakeHttpHandler(_ =>
            throw new InvalidOperationException("HTTP must not be called when token is whitespace")
        );
        var client = BuildClient(handler, ConstantTokenReader("   "));

        var result = await client.GetStatusAsync();

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("CONFIGURATION_ERROR");
    }

    /// <summary>
    /// Verifies that the CONFIGURATION_ERROR message references the token file concept,
    /// giving the caller a diagnostic hint about the root cause.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenTokenMissing_ErrorMessageMentionsTokenFile()
    {
        var handler = new FakeHttpHandler(_ =>
            throw new InvalidOperationException("HTTP must not be called")
        );
        var client = BuildClient(handler, ConstantTokenReader(null));

        var result = await client.GetStatusAsync();

        result.Error!.Message.Should().NotBeNullOrWhiteSpace();
        result
            .Error.Message.Should()
            .ContainEquivalentOf(
                "token",
                "the CONFIGURATION_ERROR message must identify the token file as the root cause"
            );
    }

    // ─── X-Request-Id header behaviour ───────────────────────────────────────────

    /// <summary>
    /// Verifies that a caller-supplied request ID is forwarded verbatim in the X-Request-Id header.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenRequestIdProvided_SendsSuppliedRequestId()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(JsonResponse(MakeStatusEnvelope("req-abc")));
        });
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        // Act
        await client.GetStatusAsync(requestId: "req-abc");

        // Assert
        captured.Should().NotBeNull();
        captured!.Headers.GetValues("X-Request-Id").Single().Should().Be("req-abc");
    }

    /// <summary>
    /// Verifies that a null request ID triggers auto-generation of a valid GUID as the
    /// X-Request-Id header value.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenRequestIdIsNull_AutoGeneratesGuidRequestId()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(JsonResponse(MakeStatusEnvelope("auto")));
        });
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        await client.GetStatusAsync(requestId: null);

        var sentId = captured!.Headers.GetValues("X-Request-Id").Single();
        Guid.TryParse(sentId, out _)
            .Should()
            .BeTrue($"auto-generated request ID '{sentId}' must be a valid GUID");
    }

    /// <summary>
    /// Verifies that a whitespace request ID is treated as absent and triggers auto-generation
    /// of a new GUID rather than forwarding the whitespace value.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenRequestIdIsWhitespace_AutoGeneratesGuidRequestId()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(JsonResponse(MakeStatusEnvelope("auto")));
        });
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        await client.GetStatusAsync(requestId: "   ");

        var sentId = captured!.Headers.GetValues("X-Request-Id").Single();
        Guid.TryParse(sentId, out _)
            .Should()
            .BeTrue("whitespace requestId must be replaced by a generated GUID");
        sentId.Should().NotBe("   ", "the whitespace value must not be forwarded");
    }

    // ─── Authorization header ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the Authorization header uses the Bearer scheme with the token returned
    /// by the token reader.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WithValidToken_SendsBearerAuthorizationHeader()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(JsonResponse(MakeStatusEnvelope("r")));
        });
        var client = BuildClient(handler, ConstantTokenReader("my-secret-token"));

        await client.GetStatusAsync();

        captured!.Headers.Authorization.Should().NotBeNull();
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("my-secret-token");
    }

    // ─── URL routing: path and query string ──────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.GetStatusAsync"/> sends a GET request
    /// to the <c>status</c> relative path.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_SendsGetRequestToStatusPath()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeStatusEnvelope("r1")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        await client.GetStatusAsync();

        capturedPath.Should().EndWith("/status");
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.ListMessagesAsync"/> sends a GET request
    /// to the Graph-shaped <c>users/{id}/messages</c> path with the <c>$filter</c>
    /// receivedDateTime lower bound and <c>$top</c> parameters.
    /// </summary>
    [TestMethod]
    public async Task ListMessagesAsync_SendsGetToMessagesPathWithFilterAndTop()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeItemsEnvelope<MessageDto>("r2")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        await client.ListMessagesAsync(
            new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            limit: 50
        );

        capturedPath.Should().Contain("users/me/messages");
        capturedPath.Should().Contain("$filter=receivedDateTime");
        capturedPath.Should().Contain("$top=50");
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.GetMessageAsync"/> sends a GET request
    /// to <c>messages/{bridgeId}</c> with special characters in the ID percent-encoded.
    /// </summary>
    [TestMethod]
    public async Task GetMessageAsync_SendsGetToMessagesPath_WithBridgeIdUrlEncoded()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeMessageEnvelope("r3")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        await client.GetMessageAsync("msg/with/slashes");

        capturedPath.Should().Contain("users/me/messages/");
        capturedPath
            .Should()
            .Contain(
                "msg%2Fwith%2Fslashes",
                "forward slashes in bridgeId must be percent-encoded so the path is unambiguous"
            );
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.ListMeetingRequestsAsync"/> sends a GET
    /// request to the Graph-shaped messages-filtered form: the <c>users/{id}/messages</c> path
    /// carrying the <c>meetingMessageType ne null</c> predicate and the <c>$top</c> parameter.
    /// </summary>
    [TestMethod]
    public async Task ListMeetingRequestsAsync_SendsGetToMessagesPathWithMeetingMessageTypeFilter()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeItemsEnvelope<MessageDto>("r4")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        await client.ListMeetingRequestsAsync(
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            limit: 25
        );

        capturedPath.Should().Contain("users/me/messages");
        capturedPath.Should().Contain("meetingMessageType");
        capturedPath.Should().Contain("$top=25");
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.ListCalendarWindowAsync"/> sends a GET
    /// request to the Graph-shaped <c>users/{id}/calendarView</c> path with
    /// <c>startDateTime</c>, <c>endDateTime</c>, and <c>$top</c>.
    /// </summary>
    [TestMethod]
    public async Task ListCalendarWindowAsync_SendsGetToCalendarViewPathWithStartEndAndTop()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeItemsEnvelope<EventDto>("r5")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        await client.ListCalendarWindowAsync(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
            limit: 75
        );

        capturedPath.Should().Contain("users/me/calendarView");
        capturedPath.Should().Contain("startDateTime=");
        capturedPath.Should().Contain("endDateTime=");
        capturedPath.Should().Contain("$top=75");
    }

    /// <summary>
    /// Verifies that <see cref="HostAdapterHttpClient.GetEventAsync"/> sends a GET request to
    /// <c>events/{bridgeId}</c> with spaces in the ID percent-encoded.
    /// </summary>
    [TestMethod]
    public async Task GetEventAsync_SendsGetToEventsPath_WithBridgeIdUrlEncoded()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeEventEnvelope("r6")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        await client.GetEventAsync("event id with spaces");

        capturedPath.Should().Contain("users/me/events/");
        capturedPath
            .Should()
            .Contain(
                "event%20id%20with%20spaces",
                "spaces in bridgeId must be percent-encoded so the path segment is valid"
            );
    }

    // ─── Default limit ────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the default limit of 100 is used when the caller omits the limit
    /// parameter on <see cref="HostAdapterHttpClient.ListMessagesAsync"/>.
    /// </summary>
    [TestMethod]
    public async Task ListMessagesAsync_WhenLimitNotSpecified_UsesDefaultLimitOf100()
    {
        string? capturedPath = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedPath = req.RequestUri!.PathAndQuery;
            return Task.FromResult(JsonResponse(MakeItemsEnvelope<MessageDto>("r7")));
        });
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        // Omit limit; default is 100 per the method signature
        await client.ListMessagesAsync(DateTimeOffset.UtcNow);

        capturedPath.Should().Contain("$top=100");
    }

    // ─── TRANSPORT_FAILURE ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an HTTP response whose JSON body deserializes to null (the JSON literal
    /// <c>null</c>) produces a TRANSPORT_FAILURE envelope.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenResponseBodyDeserializesToNull_ReturnsTransportFailure()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "null",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    ),
                }
            )
        );
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        var result = await client.GetStatusAsync();

        result
            .Ok.Should()
            .BeFalse("a null-deserialized response body must yield a failure envelope");
        result.Error!.Code.Should().Be("TRANSPORT_FAILURE");
    }

    /// <summary>
    /// Verifies that the TRANSPORT_FAILURE message includes the HTTP status code so the
    /// caller can diagnose the upstream failure without examining raw responses.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_TransportFailure_ErrorMessageIncludesHttpStatusCode()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(
                        "null",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    ),
                }
            )
        );
        var client = BuildClient(handler, ConstantTokenReader("valid-token"));

        var result = await client.GetStatusAsync();

        result
            .Error!.Message.Should()
            .Contain(
                "503",
                "the TRANSPORT_FAILURE message must include the HTTP status code for diagnostics"
            );
    }

    // ─── Successful responses ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a well-formed <see cref="ApiEnvelope{T}"/> JSON response is deserialized
    /// and returned directly by <see cref="HostAdapterHttpClient.GetStatusAsync"/>.
    /// </summary>
    [TestMethod]
    public async Task GetStatusAsync_WhenResponseIsValidEnvelope_ReturnsDeserializedEnvelope()
    {
        var expected = MakeStatusEnvelope("req-ok");
        var handler = new FakeHttpHandler(_ => Task.FromResult(JsonResponse(expected)));
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        var result = await client.GetStatusAsync(requestId: "req-ok");

        result.Ok.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.State.Should().Be("ready");
    }

    /// <summary>
    /// Verifies that a well-formed list envelope is deserialized and returned by
    /// <see cref="HostAdapterHttpClient.ListMessagesAsync"/>.
    /// </summary>
    [TestMethod]
    public async Task ListMessagesAsync_WhenResponseIsValidEnvelope_ReturnsDeserializedItems()
    {
        var expected = MakeItemsEnvelope<MessageDto>("req-msgs");
        var handler = new FakeHttpHandler(_ => Task.FromResult(JsonResponse(expected)));
        var client = BuildClient(handler, ConstantTokenReader("tok"));

        var result = await client.ListMessagesAsync(DateTimeOffset.UtcNow, requestId: "req-msgs");

        result.Ok.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty("the stub envelope carries an empty item list");
    }
}

/// <summary>
/// A minimal <see cref="HttpMessageHandler"/> that delegates each outbound request to a
/// caller-supplied function, giving unit tests full control over HTTP responses without
/// requiring network access.
/// </summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    : HttpMessageHandler
{
    /// <summary>
    /// Forwards the request to the delegate provided at construction time.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) => handler(request);
}
