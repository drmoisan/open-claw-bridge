using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// One test per remaining D5 error-matrix row: token-acquisition failure, terminal
/// HTTP statuses (400/401/403/404/500 and the unexpected-status fallback), network
/// exceptions, and the Graph <c>error.code</c> passthrough into
/// <c>ApiError.BridgeErrorCode</c>. All rows here are terminal on the first attempt,
/// so no time advancement is required.
/// </summary>
[TestClass]
public sealed class GraphRequestExecutorErrorMatrixTests
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
        IAppTokenProvider? tokenProvider = null
    ) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/v1.0/") },
            tokenProvider ?? TokenProvider(),
            new FakeTimeProvider(Start),
            new GraphAdapterOptions(),
            NullLogger.Instance
        );

    private static HttpRequestMessage NewRequest() => new(HttpMethod.Get, "users/p/messages");

    private static Task<ApiEnvelope<string>> ExecuteAsync(GraphRequestExecutor executor) =>
        executor.ExecuteAsync(NewRequest, body => body, "req-err", CancellationToken.None);

    [TestMethod]
    public async Task TokenAcquisitionException_MapsToConfigurationErrorNotRetryable()
    {
        var failing = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        failing
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new TokenAcquisitionException(
                    "tenant",
                    "client",
                    "https://graph.example.test/.default",
                    new InvalidOperationException("credential failure")
                )
            );
        var handler = new FakeHttpHandler(_ =>
            throw new AssertFailedException("No HTTP request may be sent without a token.")
        );
        var executor = Executor(handler, failing.Object);

        var result = await ExecuteAsync(executor);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("CONFIGURATION_ERROR");
        result.Error.Retryable.Should().BeFalse("a credential problem needs operator action");
        result.Meta.RequestId.Should().Be("req-err");
    }

    [DataTestMethod]
    [DataRow(HttpStatusCode.BadRequest, "INVALID_REQUEST", DisplayName = "400 -> INVALID_REQUEST")]
    [DataRow(HttpStatusCode.Unauthorized, "UNAUTHORIZED", DisplayName = "401 -> UNAUTHORIZED")]
    [DataRow(HttpStatusCode.Forbidden, "UNAUTHORIZED", DisplayName = "403 -> UNAUTHORIZED")]
    [DataRow(HttpStatusCode.NotFound, "NOT_FOUND", DisplayName = "404 -> NOT_FOUND")]
    [DataRow(
        HttpStatusCode.InternalServerError,
        "INTERNAL_ERROR",
        DisplayName = "500 -> INTERNAL_ERROR"
    )]
    public async Task TerminalStatus_MapsToMatrixCodeNotRetryable(
        HttpStatusCode status,
        string expectedCode
    )
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") })
        );
        var executor = Executor(handler);

        var result = await ExecuteAsync(executor);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        result.Error.Retryable.Should().BeFalse("terminal statuses are not retried");
        result.Error.Message.Should().Contain(((int)status).ToString());
    }

    [TestMethod]
    public async Task HttpRequestException_MapsToTransportFailureRetryable()
    {
        var handler = new FakeHttpHandler(_ =>
            throw new HttpRequestException("connection refused")
        );
        var executor = Executor(handler);

        var result = await ExecuteAsync(executor);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("TRANSPORT_FAILURE");
        result.Error.Retryable.Should().BeTrue("a network error may be transient");
        result.Meta.RequestId.Should().Be("req-err");
    }

    [TestMethod]
    public async Task UnexpectedStatus_MapsToInternalErrorNotRetryable()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage((HttpStatusCode)418) { Content = new StringContent("{}") }
            )
        );
        var executor = Executor(handler);

        var result = await ExecuteAsync(executor);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("INTERNAL_ERROR");
        result.Error.Retryable.Should().BeFalse();
        result.Error.Message.Should().Contain("418");
    }

    [TestMethod]
    public async Task GraphErrorCode_IsPreservedInBridgeErrorCode()
    {
        const string errorBody = """
            {"error":{"code":"ErrorItemNotFound","message":"The specified object was not found in the store."}}
            """;
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(errorBody),
                }
            )
        );
        var executor = Executor(handler);

        var result = await ExecuteAsync(executor);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
        result
            .Error.BridgeErrorCode.Should()
            .Be("ErrorItemNotFound", "the Graph error.code passes through as the backend code");
    }

    [TestMethod]
    public async Task UnparseableErrorBody_YieldsNullBridgeErrorCode()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("<html>gateway page</html>"),
                }
            )
        );
        var executor = Executor(handler);

        var result = await ExecuteAsync(executor);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
        result.Error.BridgeErrorCode.Should().BeNull("no Graph error body means no passthrough");
    }
}
