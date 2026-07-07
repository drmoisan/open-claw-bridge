using System;
using System.Net;
using System.Net.Http;
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

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Contract tests for <see cref="GraphMailboxScopeProbe"/> (spec D2, AC-5) against the
/// existing <see cref="FakeHttpHandler"/> — no network. Verifies request shape (relative
/// path, UPN escaping, <c>$top=1&amp;$select=id</c>, bearer header, <c>client-request-id</c>),
/// the envelope-to-outcome projection (200 empty value → Ok; 403 ErrorAccessDenied; 401
/// InvalidAuthenticationToken; unparseable error body → null BridgeErrorCode), and
/// cancellation-token flow-through into the executor.
/// </summary>
[TestClass]
public sealed class GraphMailboxScopeProbeTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

    private static Mock<IAppTokenProvider> TokenProviderMock()
    {
        var mock = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        mock.Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppAccessToken("secret-bearer-token", Start.AddHours(1)));
        return mock;
    }

    private static GraphMailboxScopeProbe BuildProbe(
        FakeHttpHandler handler,
        IAppTokenProvider? tokenProvider = null
    )
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.example.test/v1.0/"),
        };
        return new GraphMailboxScopeProbe(
            httpClient,
            Options.Create(new GraphAdapterOptions()),
            tokenProvider ?? TokenProviderMock().Object,
            new FakeTimeProvider(Start),
            NullLogger<GraphMailboxScopeProbe>.Instance
        );
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    // ─── Request shape ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ProbeMailboxReadAsync_SendsGetToMessagesPathWithTopAndSelect()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(Response(HttpStatusCode.OK, "{\"value\":[]}"));
        });
        var probe = BuildProbe(handler);

        await probe.ProbeMailboxReadAsync("in-scope@contoso.com", requestId: "req-shape");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Get);
        var pathAndQuery = captured.RequestUri!.PathAndQuery;
        pathAndQuery.Should().Contain("users/in-scope%40contoso.com/messages");
        pathAndQuery.Should().Contain("$top=1");
        pathAndQuery.Should().Contain("$select=id");
    }

    [TestMethod]
    public async Task ProbeMailboxReadAsync_EscapesReservedCharactersInUpn()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(Response(HttpStatusCode.OK, "{\"value\":[]}"));
        });
        var probe = BuildProbe(handler);

        await probe.ProbeMailboxReadAsync("test user+tag@contoso.com");

        var pathAndQuery = captured!.RequestUri!.PathAndQuery;
        pathAndQuery
            .Should()
            .Contain(
                "users/test%20user%2Btag%40contoso.com/messages",
                "reserved characters in the UPN must be percent-encoded"
            );
    }

    [TestMethod]
    public async Task ProbeMailboxReadAsync_SendsBearerAuthorizationAndClientRequestIdHeaders()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpHandler(req =>
        {
            captured = req;
            return Task.FromResult(Response(HttpStatusCode.OK, "{\"value\":[]}"));
        });
        var probe = BuildProbe(handler);

        await probe.ProbeMailboxReadAsync("in-scope@contoso.com", requestId: "req-headers");

        captured!.Headers.Authorization.Should().NotBeNull();
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("secret-bearer-token");
        captured
            .Headers.GetValues("client-request-id")
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("req-headers");
    }

    // ─── Envelope-to-outcome projection ──────────────────────────────────────────

    [TestMethod]
    public async Task ProbeMailboxReadAsync_200EmptyValue_IsOk()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Response(HttpStatusCode.OK, "{\"value\":[]}"))
        );
        var probe = BuildProbe(handler);

        var outcome = await probe.ProbeMailboxReadAsync("in-scope@contoso.com");

        outcome.Ok.Should().BeTrue("a 200 with an empty value array is an authorized read");
        outcome.ErrorCode.Should().BeNull();
        outcome.BridgeErrorCode.Should().BeNull();
    }

    [TestMethod]
    public async Task ProbeMailboxReadAsync_403ErrorAccessDenied_MapsToUnauthorizedDenial()
    {
        const string body =
            "{\"error\":{\"code\":\"ErrorAccessDenied\",\"message\":\"Access is denied.\"}}";
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Response(HttpStatusCode.Forbidden, body))
        );
        var probe = BuildProbe(handler);

        var outcome = await probe.ProbeMailboxReadAsync("out-of-scope@contoso.com");

        outcome.Ok.Should().BeFalse();
        outcome.ErrorCode.Should().Be("UNAUTHORIZED");
        outcome.BridgeErrorCode.Should().Be("ErrorAccessDenied");
        outcome.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task ProbeMailboxReadAsync_401InvalidAuthenticationToken_MapsToUnauthorizedWith401Code()
    {
        const string body =
            "{\"error\":{\"code\":\"InvalidAuthenticationToken\",\"message\":\"Access token has expired.\"}}";
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Response(HttpStatusCode.Unauthorized, body))
        );
        var probe = BuildProbe(handler);

        var outcome = await probe.ProbeMailboxReadAsync("out-of-scope@contoso.com");

        outcome.Ok.Should().BeFalse();
        outcome
            .ErrorCode.Should()
            .Be("UNAUTHORIZED", "the executor folds 401 and 403 into UNAUTHORIZED");
        outcome
            .BridgeErrorCode.Should()
            .Be(
                "InvalidAuthenticationToken",
                "the 401 Graph code discriminates auth failure from RBAC denial"
            );
    }

    [TestMethod]
    public async Task ProbeMailboxReadAsync_UnparseableErrorBody_YieldsNullBridgeErrorCode()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Response(HttpStatusCode.Forbidden, "<html>gateway denial page</html>"))
        );
        var probe = BuildProbe(handler);

        var outcome = await probe.ProbeMailboxReadAsync("out-of-scope@contoso.com");

        outcome.Ok.Should().BeFalse();
        outcome.ErrorCode.Should().Be("UNAUTHORIZED");
        outcome
            .BridgeErrorCode.Should()
            .BeNull("no parseable Graph error body means no passthrough code");
    }

    // ─── Cancellation flow-through ───────────────────────────────────────────────

    [TestMethod]
    public async Task ProbeMailboxReadAsync_FlowsCancellationTokenIntoExecutor()
    {
        CancellationToken observed = default;
        var tokenMock = new Mock<IAppTokenProvider>(MockBehavior.Strict);
        tokenMock
            .Setup(p => p.GetTokenAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(ct => observed = ct)
            .ReturnsAsync(new AppAccessToken("tok", Start.AddHours(1)));
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(Response(HttpStatusCode.OK, "{\"value\":[]}"))
        );
        var probe = BuildProbe(handler, tokenMock.Object);
        using var cts = new CancellationTokenSource();

        await probe.ProbeMailboxReadAsync("in-scope@contoso.com", cancellationToken: cts.Token);

        observed
            .Should()
            .Be(cts.Token, "the cancellation token flows through into the Graph executor");
    }
}
