using Azure.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;

namespace OpenClaw.Core.Tests.CloudAuth;

/// <summary>
/// Part 2 of the <see cref="ClientCredentialsTokenProvider"/> suite (500-line split of
/// <c>ClientCredentialsTokenProviderTests.cs</c>): single-flight concurrency
/// (TaskCompletionSource-gated, no timing), unwrapped cancellation at both await
/// points, D7 failure wrapping with non-secret context, fail-closed stale-cache
/// behavior, and recovery after failure. All time via <see cref="FakeTimeProvider"/>.
/// </summary>
[TestClass]
public sealed class ClientCredentialsTokenProviderConcurrencyTests
{
    private const string FakeTenantId = "00000000-0000-0000-0000-000000000001";
    private const string FakeClientId = "00000000-0000-0000-0000-000000000002";
    private const string FakeCertificatePath = "/run/secrets/fake-cert.pem";
    private const string FakeToken = "fake-token-value";

    private static readonly DateTimeOffset Start = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static CloudAuthOptions Options() =>
        new()
        {
            TenantId = FakeTenantId,
            ClientId = FakeClientId,
            CertificatePath = FakeCertificatePath,
        };

    private static ClientCredentialsTokenProvider Provider(
        Mock<TokenCredential> credential,
        FakeTimeProvider timeProvider
    ) =>
        new(
            credential.Object,
            Options(),
            timeProvider,
            NullLogger<ClientCredentialsTokenProvider>.Instance
        );

    // --- (a) Single-flight ---

    [TestMethod]
    public async Task GetTokenAsync_EightConcurrentCallersWithStaleCache_MakeExactlyOneCredentialCall()
    {
        // Arrange: the mocked credential parks every call on a TaskCompletionSource so
        // the refresh is in flight while the remaining callers queue on the semaphore.
        // No timing is involved: the callers are started sequentially on this thread,
        // and the gate is completed only afterwards.
        var gate = new TaskCompletionSource<AccessToken>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var callCount = 0;
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (TokenRequestContext _, CancellationToken _) =>
                {
                    Interlocked.Increment(ref callCount);
                    return new ValueTask<AccessToken>(gate.Task);
                }
            );
        var provider = Provider(credential, new FakeTimeProvider(Start));

        // Act: launch 8 callers against the empty (stale) cache, then release the gate.
        const int callers = 8;
        var pending = new Task<AppAccessToken>[callers];
        for (var i = 0; i < callers; i++)
        {
            pending[i] = provider.GetTokenAsync(CancellationToken.None).AsTask();
        }

        gate.SetResult(new AccessToken(FakeToken, Start.AddMinutes(30)));
        var tokens = await Task.WhenAll(pending);

        // Assert: exactly one credential call; every caller observes the refreshed token.
        callCount.Should().Be(1, "single-flight: one credential call per staleness window");
        tokens
            .Should()
            .AllSatisfy(t =>
            {
                t.Token.Should().Be(FakeToken);
                t.ExpiresOn.Should().Be(Start.AddMinutes(30));
            });
    }

    // --- (b) Cancellation before the semaphore wait ---

    [TestMethod]
    public async Task GetTokenAsync_CancelledBeforeSemaphoreWait_ThrowsUnwrappedAndLeavesCacheUnchanged()
    {
        // Arrange: seed the cache, advance past the skew boundary so it is stale, then
        // call with an already-cancelled token — the semaphore wait throws before any
        // credential call.
        var timeProvider = new FakeTimeProvider(Start);
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(() =>
                new AccessToken(FakeToken, timeProvider.GetUtcNow().AddMinutes(30))
            );
        var provider = Provider(credential, timeProvider);
        await provider.GetTokenAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(26));

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        var act = async () => await provider.GetTokenAsync(cancelled.Token);

        // Assert: unwrapped cancellation; no second credential call happened.
        await act.Should().ThrowAsync<OperationCanceledException>();
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Assert cache unchanged: the stale token is still stale, so the next
        // successful call refreshes (second credential call).
        await provider.GetTokenAsync(CancellationToken.None);
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    // --- (c) Cancellation during the credential call ---

    [TestMethod]
    public async Task GetTokenAsync_CancelledDuringCredentialCall_ThrowsUnwrappedAndLeavesCacheUnchanged()
    {
        // Arrange: seed the cache, go stale, then have the credential honor the
        // cancellation token deterministically on its next invocation.
        var timeProvider = new FakeTimeProvider(Start);
        var throwCancellation = false;
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (TokenRequestContext _, CancellationToken _) =>
                    throwCancellation
                        ? throw new OperationCanceledException()
                        : new ValueTask<AccessToken>(
                            new AccessToken(FakeToken, timeProvider.GetUtcNow().AddMinutes(30))
                        )
            );
        var provider = Provider(credential, timeProvider);
        await provider.GetTokenAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(26));
        throwCancellation = true;

        // Act
        var act = async () => await provider.GetTokenAsync(CancellationToken.None);

        // Assert: OperationCanceledException surfaces unwrapped (never
        // TokenAcquisitionException).
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Assert cache unchanged: the next successful call performs a real refresh.
        throwCancellation = false;
        await provider.GetTokenAsync(CancellationToken.None);
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
    }

    // --- (d) + (e) Failure propagation with context, no secret material ---

    [TestMethod]
    public async Task GetTokenAsync_CredentialFailure_SurfacesTokenAcquisitionExceptionWithContext()
    {
        // Arrange
        var inner = new InvalidOperationException("underlying credential failure");
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(inner);
        var provider = Provider(credential, new FakeTimeProvider(Start));

        // Act
        var act = async () => await provider.GetTokenAsync(CancellationToken.None);

        // Assert: tenant/client/scope context, inner preserved, no secret material.
        var thrown = (await act.Should().ThrowAsync<TokenAcquisitionException>()).Which;
        thrown.TenantId.Should().Be(FakeTenantId);
        thrown.ClientId.Should().Be(FakeClientId);
        thrown.Scope.Should().Be("https://graph.microsoft.com/.default");
        thrown.InnerException.Should().BeSameAs(inner);
        thrown.Message.Should().NotContain(FakeToken).And.NotContain(FakeCertificatePath);
    }

    // --- (f) No stale token after failed refresh ---

    [TestMethod]
    public async Task GetTokenAsync_FailedRefreshWithStaleCachedToken_ThrowsInsteadOfServingStale()
    {
        // Arrange: seed the cache, go stale, then make the credential fail.
        var timeProvider = new FakeTimeProvider(Start);
        var fail = false;
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (TokenRequestContext _, CancellationToken _) =>
                    fail
                        ? throw new InvalidOperationException("refresh failed")
                        : new ValueTask<AccessToken>(
                            new AccessToken(FakeToken, timeProvider.GetUtcNow().AddMinutes(30))
                        )
            );
        var provider = Provider(credential, timeProvider);
        await provider.GetTokenAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(26));
        fail = true;

        // Act
        var act = async () => await provider.GetTokenAsync(CancellationToken.None);

        // Assert: fail-closed — the stale cached token is not returned.
        await act.Should()
            .ThrowAsync<TokenAcquisitionException>(
                "a failed refresh must never serve the stale cached token"
            );
    }

    // --- (g) Recovery after failure ---

    [TestMethod]
    public async Task GetTokenAsync_SuccessfulCallAfterFailure_ReturnsFreshToken()
    {
        // Arrange: first credential call fails, second succeeds.
        var timeProvider = new FakeTimeProvider(Start);
        var fail = true;
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (TokenRequestContext _, CancellationToken _) =>
                    fail
                        ? throw new InvalidOperationException("first attempt fails")
                        : new ValueTask<AccessToken>(
                            new AccessToken(FakeToken, timeProvider.GetUtcNow().AddMinutes(30))
                        )
            );
        var provider = Provider(credential, timeProvider);
        var failingCall = async () => await provider.GetTokenAsync(CancellationToken.None);
        await failingCall.Should().ThrowAsync<TokenAcquisitionException>();

        // Act: the credential recovers; the provider must acquire a fresh token.
        fail = false;
        var token = await provider.GetTokenAsync(CancellationToken.None);

        // Assert
        token.Token.Should().Be(FakeToken);
        token.ExpiresOn.Should().Be(timeProvider.GetUtcNow().AddMinutes(30));
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }
}
