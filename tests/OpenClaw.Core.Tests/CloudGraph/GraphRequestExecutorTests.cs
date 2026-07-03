using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Handler-level tests for <see cref="GraphRequestExecutor"/>: bearer auth from the
/// mocked <see cref="IAppTokenProvider"/> on every attempt, <c>client-request-id</c>
/// generation and echo into <c>ApiMeta.RequestId</c>, one request-factory invocation
/// per attempt (a fresh <see cref="HttpRequestMessage"/> each time), success-envelope
/// shape, and the 2xx-with-unparseable-body mapping. No wall-clock waits: retries
/// advance a <see cref="FakeTimeProvider"/> only.
/// </summary>
[TestClass]
public sealed class GraphRequestExecutorTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

    private static Mock<IAppTokenProvider> TokenProvider(string token = "tok-A")
    {
        var mock = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken(token, Start.AddHours(1)));
        return mock;
    }

    private static GraphRequestExecutor Executor(
        FakeHttpHandler handler,
        IAppTokenProvider tokenProvider,
        TimeProvider timeProvider,
        GraphAdapterOptions? options = null
    ) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            tokenProvider,
            timeProvider,
            options ?? new GraphAdapterOptions(),
            NullLogger.Instance
        );

    private static HttpResponseMessage Response(HttpStatusCode status, string body = "{}") =>
        new(status) { Content = new StringContent(body) };

    private static HttpRequestMessage NewRequest() =>
        new(HttpMethod.Get, "users/p%40contoso.com/messages");

    /// <summary>
    /// Awaits <paramref name="task"/> while advancing only the fake clock, so retry
    /// backoff completes without any real sleep.
    /// </summary>
    private static async Task<T> AwaitWithTimeAdvance<T>(
        Task<T> task,
        FakeTimeProvider timeProvider,
        TimeSpan step
    )
    {
        var safety = 0;
        while (!task.IsCompleted)
        {
            if (++safety > 10_000)
            {
                throw new AssertFailedException(
                    "The executor task did not complete under fake-time advancement."
                );
            }

            timeProvider.Advance(step);
            await Task.Yield();
        }

        return await task;
    }

    [TestMethod]
    public async Task Success_SynthesizesSuccessEnvelopeShape()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Response(HttpStatusCode.OK, """{"kind":"probe"}"""))
        );
        var executor = Executor(handler, TokenProvider().Object, new FakeTimeProvider(Start));

        var result = await executor.ExecuteAsync(
            NewRequest,
            body => "parsed:" + body,
            "req-1",
            CancellationToken.None
        );

        result.Ok.Should().BeTrue();
        result.Data.Should().Be("""parsed:{"kind":"probe"}""");
        result.Meta.RequestId.Should().Be("req-1");
        result.Meta.AdapterVersion.Should().Be("cloudgraph");
        result.Meta.Bridge.Should().BeNull("there is no bridge behind the Graph backend");
        result.Error.Should().BeNull();
    }

    [TestMethod]
    public async Task CallerRequestId_SentAsClientRequestIdHeader()
    {
        string? headerValue = null;
        var handler = new FakeHttpHandler(request =>
        {
            headerValue = string.Join(",", request.Headers.GetValues("client-request-id"));
            return Task.FromResult(Response(HttpStatusCode.OK));
        });
        var executor = Executor(handler, TokenProvider().Object, new FakeTimeProvider(Start));

        var result = await executor.ExecuteAsync(
            NewRequest,
            body => body,
            "req-42",
            CancellationToken.None
        );

        headerValue.Should().Be("req-42", "the caller-supplied request id goes to Graph");
        result.Meta.RequestId.Should().Be("req-42", "the same id is echoed into ApiMeta");
    }

    [DataTestMethod]
    [DataRow(null, DisplayName = "null requestId")]
    [DataRow("   ", DisplayName = "blank requestId")]
    public async Task NullOrBlankRequestId_GeneratesGuidAndEchoesIt(string? requestId)
    {
        string? headerValue = null;
        var handler = new FakeHttpHandler(request =>
        {
            headerValue = string.Join(",", request.Headers.GetValues("client-request-id"));
            return Task.FromResult(Response(HttpStatusCode.OK));
        });
        var executor = Executor(handler, TokenProvider().Object, new FakeTimeProvider(Start));

        var result = await executor.ExecuteAsync(
            NewRequest,
            body => body,
            requestId,
            CancellationToken.None
        );

        Guid.TryParse(result.Meta.RequestId, out _)
            .Should()
            .BeTrue("a blank caller id is replaced by a generated GUID");
        headerValue.Should().Be(result.Meta.RequestId, "the generated id is both sent and echoed");
    }

    [TestMethod]
    public async Task BearerToken_SourcedFromTokenProviderOnEveryAttempt()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var authHeaders = new List<string?>();
        var attempt = 0;
        var handler = new FakeHttpHandler(request =>
        {
            authHeaders.Add(request.Headers.Authorization?.ToString());
            attempt++;
            return Task.FromResult(
                attempt < 3
                    ? Response(HttpStatusCode.ServiceUnavailable)
                    : Response(HttpStatusCode.OK)
            );
        });
        var tokenProvider = TokenProvider("tok-A");
        var executor = Executor(handler, tokenProvider.Object, timeProvider);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-b", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeTrue();
        authHeaders
            .Should()
            .Equal(
                new[] { "Bearer tok-A", "Bearer tok-A", "Bearer tok-A" },
                "each attempt re-acquires and re-applies the bearer token"
            );
        tokenProvider.Verify(
            p => p.GetTokenAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(3),
            "the token is obtained per attempt"
        );
    }

    [TestMethod]
    public async Task RequestFactory_InvokedOncePerAttempt_WithFreshRequestEachTime()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attempt = 0;
        var handler = new FakeHttpHandler(_ =>
        {
            attempt++;
            return Task.FromResult(
                attempt < 3
                    ? Response(HttpStatusCode.ServiceUnavailable)
                    : Response(HttpStatusCode.OK)
            );
        });
        var created = new List<HttpRequestMessage>();
        var executor = Executor(handler, TokenProvider().Object, timeProvider);

        var task = executor.ExecuteAsync(
            () =>
            {
                var request = NewRequest();
                created.Add(request);
                return request;
            },
            body => body,
            "req-f",
            CancellationToken.None
        );
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeTrue();
        created.Should().HaveCount(3, "the factory builds one request per attempt");
        created
            .Should()
            .OnlyHaveUniqueItems("an HttpRequestMessage is single-use and rebuilt per attempt");
    }

    [TestMethod]
    public async Task SuccessWithUnparseableBody_MapsToTransportFailureNotRetryable()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Response(HttpStatusCode.OK, "<html>not json</html>"))
        );
        var executor = Executor(handler, TokenProvider().Object, new FakeTimeProvider(Start));

        var result = await executor.ExecuteAsync(
            NewRequest,
            body =>
                JsonSerializer.Deserialize<GraphErrorBody>(body, GraphRequestExecutor.JsonOptions)
                ?? throw new JsonException("null body"),
            "req-u",
            CancellationToken.None
        );

        result.Ok.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Error.Retryable.Should().BeFalse("an unparseable 2xx body is terminal");
        result.Error.Message.Should().Contain("200").And.Contain("parseable");
        result.Meta.RequestId.Should().Be("req-u");
    }

    [TestMethod]
    public async Task SuccessBodyMappingFailure_MapsToInternalError()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(Response(HttpStatusCode.OK)));
        var executor = Executor(handler, TokenProvider().Object, new FakeTimeProvider(Start));

        var result = await executor.ExecuteAsync<string>(
            NewRequest,
            _ => throw new GraphMappingException("The Graph message is missing 'id'."),
            "req-m",
            CancellationToken.None
        );

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("INTERNAL_ERROR");
        result.Error.Retryable.Should().BeFalse();
        result.Error.Message.Should().Be("The Graph message is missing 'id'.");
    }
}
