using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
/// Retry/backoff tests for <see cref="GraphRequestExecutor"/> driven exclusively by
/// <see cref="FakeTimeProvider"/> advancement: <c>Retry-After</c> delta-seconds and
/// HTTP-date forms honored exactly and taking precedence over the exponential
/// fallback, the 1s/2s/4s default fallback schedule, the <c>MaxDelaySeconds</c> cap,
/// 429-then-success recovery, and 429/50x exhaustion envelopes. Attempt timestamps are
/// recorded from the fake clock, so assertions are exact and no real sleep occurs.
/// </summary>
[TestClass]
public sealed class GraphRequestExecutorRetryTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

    private static IAppTokenProvider TokenProvider()
    {
        var mock = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("tok", Start.AddHours(1)));
        return mock.Object;
    }

    private static GraphRequestExecutor Executor(
        FakeHttpHandler handler,
        TimeProvider timeProvider,
        GraphAdapterOptions? options = null
    ) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            TokenProvider(),
            timeProvider,
            options ?? new GraphAdapterOptions(),
            NullLogger.Instance
        );

    private static HttpRequestMessage NewRequest() => new(HttpMethod.Get, "users/p/messages");

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

    /// <summary>
    /// Builds a handler that records the fake-clock timestamp of every attempt and
    /// serves the queued responses in order (the last response repeats).
    /// </summary>
    private static FakeHttpHandler RecordingHandler(
        FakeTimeProvider timeProvider,
        List<DateTimeOffset> attemptTimes,
        params Func<HttpResponseMessage>[] responses
    )
    {
        var index = 0;
        return new FakeHttpHandler(_ =>
        {
            attemptTimes.Add(timeProvider.GetUtcNow());
            var response = responses[Math.Min(index, responses.Length - 1)]();
            index++;
            return Task.FromResult(response);
        });
    }

    private static HttpResponseMessage Status(HttpStatusCode status) =>
        new(status) { Content = new StringContent("{}") };

    private static HttpResponseMessage ThrottledDelta(int seconds)
    {
        var response = Status(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        return response;
    }

    [TestMethod]
    public async Task RetryAfterDeltaSeconds_IsHonoredExactly()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(
            timeProvider,
            attemptTimes,
            () => ThrottledDelta(7),
            () => Status(HttpStatusCode.OK)
        );
        var executor = Executor(handler, timeProvider);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-d", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeTrue();
        attemptTimes
            .Should()
            .Equal(
                new[] { Start, Start.AddSeconds(7) },
                "the second attempt waits exactly the Retry-After delta"
            );
    }

    [TestMethod]
    public async Task RetryAfterHttpDate_IsHonoredRelativeToFakeUtcNow()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(
            timeProvider,
            attemptTimes,
            () =>
            {
                var response = Status(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(Start.AddSeconds(9));
                return response;
            },
            () => Status(HttpStatusCode.OK)
        );
        var executor = Executor(handler, timeProvider);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-h", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeTrue();
        attemptTimes
            .Should()
            .Equal(
                new[] { Start, Start.AddSeconds(9) },
                "the HTTP-date form is evaluated against the injected clock's UtcNow"
            );
    }

    [TestMethod]
    public async Task RetryAfter_TakesPrecedenceOverExponentialFallback()
    {
        // Default options give a 1s exponential first delay; Retry-After: 10 must win.
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(
            timeProvider,
            attemptTimes,
            () => ThrottledDelta(10),
            () => Status(HttpStatusCode.OK)
        );
        var executor = Executor(handler, timeProvider);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-p", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeTrue();
        attemptTimes
            .Should()
            .Equal(
                new[] { Start, Start.AddSeconds(10) },
                "the server's Retry-After wins over the 1s exponential fallback"
            );
    }

    [TestMethod]
    public async Task ExponentialFallback_YieldsOneTwoFourSecondsWithDefaultOptions()
    {
        // Four attempts (default MaxAttempts) with no Retry-After header: delays are
        // 1s, 2s, 4s, so attempts land at +0s, +1s, +3s, +7s.
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(
            timeProvider,
            attemptTimes,
            () => Status(HttpStatusCode.TooManyRequests)
        );
        var executor = Executor(handler, timeProvider);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-e", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeFalse();
        attemptTimes
            .Should()
            .Equal(
                new[] { Start, Start.AddSeconds(1), Start.AddSeconds(3), Start.AddSeconds(7) },
                "the exponential fallback schedule is BaseDelay * 2^(attempt-1)"
            );
    }

    [TestMethod]
    public async Task ExponentialFallback_IsCappedAtMaxDelaySeconds()
    {
        // Base 10s doubles to 20s but MaxDelaySeconds caps it at 15s: attempts land
        // at +0s, +10s, +25s.
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(
            timeProvider,
            attemptTimes,
            () => Status(HttpStatusCode.ServiceUnavailable)
        );
        var options = new GraphAdapterOptions
        {
            MaxAttempts = 3,
            BaseDelaySeconds = 10,
            MaxDelaySeconds = 15,
        };
        var executor = Executor(handler, timeProvider, options);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-c", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeFalse();
        attemptTimes
            .Should()
            .Equal(
                new[] { Start, Start.AddSeconds(10), Start.AddSeconds(25) },
                "the second delay (20s uncapped) is capped at MaxDelaySeconds (15s)"
            );
    }

    [TestMethod]
    public async Task Throttled429ThenSuccess_RecoversWithSuccessEnvelope()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(
            timeProvider,
            attemptTimes,
            () => Status(HttpStatusCode.TooManyRequests),
            () =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("recovered"),
                }
        );
        var executor = Executor(handler, timeProvider);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-r", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeTrue();
        result.Data.Should().Be("recovered");
        result.Error.Should().BeNull();
        attemptTimes.Should().HaveCount(2, "one throttled attempt then one success");
    }

    [TestMethod]
    public async Task Exhaustion429_ReturnsThrottledRetryableWithRequestIdAndAttemptCount()
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(
            timeProvider,
            attemptTimes,
            () => Status(HttpStatusCode.TooManyRequests)
        );
        var executor = Executor(handler, timeProvider);

        var task = executor.ExecuteAsync(
            NewRequest,
            body => body,
            "req-th",
            CancellationToken.None
        );
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("THROTTLED");
        result.Error.Retryable.Should().BeTrue("throttling exhaustion is safe to retry later");
        result.Error.Message.Should().Contain("4", "the message records the attempt count");
        result.Meta.RequestId.Should().Be("req-th");
        attemptTimes.Should().HaveCount(4, "the default MaxAttempts is 4");
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.BadGateway, DisplayName = "502 exhaustion")]
    [DataRow(HttpStatusCode.ServiceUnavailable, DisplayName = "503 exhaustion")]
    [DataRow(HttpStatusCode.GatewayTimeout, DisplayName = "504 exhaustion")]
    public async Task Exhaustion50x_ReturnsTransportFailureRetryable(HttpStatusCode status)
    {
        var timeProvider = new FakeTimeProvider(Start);
        var attemptTimes = new List<DateTimeOffset>();
        var handler = RecordingHandler(timeProvider, attemptTimes, () => Status(status));
        var options = new GraphAdapterOptions { MaxAttempts = 2 };
        var executor = Executor(handler, timeProvider, options);

        var task = executor.ExecuteAsync(NewRequest, body => body, "req-x", CancellationToken.None);
        var result = await AwaitWithTimeAdvance(task, timeProvider, TimeSpan.FromSeconds(1));

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Error.Retryable.Should().BeTrue("gateway failures are retryable after exhaustion");
        attemptTimes.Should().HaveCount(2);
    }
}
