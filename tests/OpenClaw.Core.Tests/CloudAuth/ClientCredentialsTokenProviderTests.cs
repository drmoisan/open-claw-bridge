using Azure.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenClaw.Core.CloudAuth;

namespace OpenClaw.Core.Tests.CloudAuth;

/// <summary>
/// Construction fail-closed behavior and deterministic caching for
/// <see cref="ClientCredentialsTokenProvider"/> (D4/D6): the public constructor
/// rejects invalid options with the full violation list; caching, expiry-refresh, and
/// the skew boundary are exercised with a mocked <see cref="TokenCredential"/> and
/// <see cref="FakeTimeProvider"/> — no wall clock, no sleeps. Concurrency,
/// cancellation, and failure propagation live in
/// <c>ClientCredentialsTokenProviderConcurrencyTests.cs</c> (500-line split branch).
/// </summary>
[TestClass]
public sealed class ClientCredentialsTokenProviderTests
{
    private const string FakeTenantId = "00000000-0000-0000-0000-000000000001";
    private const string FakeClientId = "00000000-0000-0000-0000-000000000002";
    private const string FakeCertificatePath = "/run/secrets/fake-cert.pem";
    private const string FakeClientSecret = "fake-client-secret-value";
    private const string FakeToken = "fake-token-value";

    private static readonly DateTimeOffset Start = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static CloudAuthOptions ValidCertificateOptions() =>
        new()
        {
            TenantId = FakeTenantId,
            ClientId = FakeClientId,
            CertificatePath = FakeCertificatePath,
        };

    private static CloudAuthOptions ValidSecretOptions() =>
        new()
        {
            TenantId = FakeTenantId,
            ClientId = FakeClientId,
            ClientSecret = FakeClientSecret,
        };

    private static Mock<TokenCredential> CredentialReturning(DateTimeOffset expiresOn)
    {
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new AccessToken(FakeToken, expiresOn));
        return credential;
    }

    private static ClientCredentialsTokenProvider Provider(
        Mock<TokenCredential> credential,
        CloudAuthOptions options,
        FakeTimeProvider timeProvider
    ) =>
        new(
            credential.Object,
            options,
            timeProvider,
            NullLogger<ClientCredentialsTokenProvider>.Instance
        );

    // --- (a) Public-constructor fail-closed matrix ---

    private static void MutateBlankTenant(CloudAuthOptions o) => o.TenantId = " ";

    private static void MutateBlankClient(CloudAuthOptions o) => o.ClientId = "";

    private static void MutateNeitherSource(CloudAuthOptions o) => o.CertificatePath = "";

    private static void MutateBothSources(CloudAuthOptions o) => o.ClientSecret = FakeClientSecret;

    private static void MutateBadSkew(CloudAuthOptions o) => o.RefreshSkewMinutes = 61;

    [DataTestMethod]
    [DataRow(nameof(MutateBlankTenant), "TenantId is required")]
    [DataRow(nameof(MutateBlankClient), "ClientId is required")]
    [DataRow(nameof(MutateNeitherSource), "neither is set")]
    [DataRow(nameof(MutateBothSources), "both are set")]
    [DataRow(nameof(MutateBadSkew), "RefreshSkewMinutes")]
    public void Constructor_InvalidOptions_ThrowsWithViolationMessage(
        string mutationName,
        string expectedFragment
    )
    {
        // Arrange: start from valid certificate options and apply one invalidating mutation.
        var options = ValidCertificateOptions();
        Action<CloudAuthOptions> mutate = mutationName switch
        {
            nameof(MutateBlankTenant) => MutateBlankTenant,
            nameof(MutateBlankClient) => MutateBlankClient,
            nameof(MutateNeitherSource) => MutateNeitherSource,
            nameof(MutateBothSources) => MutateBothSources,
            _ => MutateBadSkew,
        };
        mutate(options);

        // Act
        var act = () =>
            new ClientCredentialsTokenProvider(
                options,
                new FakeTimeProvider(Start),
                NullLogger<ClientCredentialsTokenProvider>.Instance
            );

        // Assert: fail-closed at construction with the named violation, no value echo.
        act.Should()
            .Throw<ArgumentException>()
            .Which.Message.Should()
            .Contain(expectedFragment)
            .And.NotContain(FakeClientSecret)
            .And.NotContain(FakeCertificatePath);
    }

    [TestMethod]
    public void Constructor_MultipleViolations_MessageCarriesAllOfThem()
    {
        // Arrange: blank tenant + blank client + no source + bad skew simultaneously.
        var options = new CloudAuthOptions
        {
            TenantId = "",
            ClientId = " ",
            RefreshSkewMinutes = -1,
        };

        // Act
        var act = () =>
            new ClientCredentialsTokenProvider(
                options,
                new FakeTimeProvider(Start),
                NullLogger<ClientCredentialsTokenProvider>.Instance
            );

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .Which.Message.Should()
            .Contain("TenantId is required")
            .And.Contain("ClientId is required")
            .And.Contain("neither is set")
            .And.Contain("RefreshSkewMinutes");
    }

    // --- (b) Public-constructor credential-source selection (construction only) ---

    [TestMethod]
    public void Constructor_CertificateOnlyOptions_ConstructsSuccessfully()
    {
        // Arrange + Act: certificate credential creation is lazy; construction must
        // succeed without touching the (fake) certificate file.
        var provider = new ClientCredentialsTokenProvider(
            ValidCertificateOptions(),
            new FakeTimeProvider(Start),
            NullLogger<ClientCredentialsTokenProvider>.Instance
        );

        // Assert
        provider.Should().NotBeNull("certificate-only options are a valid credential source");
    }

    [TestMethod]
    public void Constructor_SecretOnlyOptions_ConstructsSuccessfully()
    {
        // Arrange + Act
        var provider = new ClientCredentialsTokenProvider(
            ValidSecretOptions(),
            new FakeTimeProvider(Start),
            NullLogger<ClientCredentialsTokenProvider>.Instance
        );

        // Assert
        provider.Should().NotBeNull("secret-only options are the documented fallback source");
    }

    // --- (c) Success: first call maps the credential result ---

    [TestMethod]
    public async Task GetTokenAsync_FirstCall_ReturnsMappedTokenAndExpiry()
    {
        // Arrange
        var expiresOn = Start.AddHours(1);
        var credential = CredentialReturning(expiresOn);
        var provider = Provider(credential, ValidCertificateOptions(), new FakeTimeProvider(Start));

        // Act
        var token = await provider.GetTokenAsync(CancellationToken.None);

        // Assert
        token.Token.Should().Be(FakeToken);
        token.ExpiresOn.Should().Be(expiresOn);
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task GetTokenAsync_RequestsTheConfiguredScope()
    {
        // Arrange
        var credential = CredentialReturning(Start.AddHours(1));
        var provider = Provider(credential, ValidCertificateOptions(), new FakeTimeProvider(Start));

        // Act
        await provider.GetTokenAsync(CancellationToken.None);

        // Assert: the default Graph .default scope flows into the request context.
        credential.Verify(
            c =>
                c.GetTokenAsync(
                    It.Is<TokenRequestContext>(ctx =>
                        ctx.Scopes.Length == 1
                        && ctx.Scopes[0] == "https://graph.microsoft.com/.default"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // --- (d) Cache-hit ---

    [TestMethod]
    public async Task GetTokenAsync_SecondCallBeforeSkewBoundary_ReturnsCachedTokenWithOneCall()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Start);
        var credential = CredentialReturning(Start.AddMinutes(30));
        var provider = Provider(credential, ValidCertificateOptions(), timeProvider);
        var first = await provider.GetTokenAsync(CancellationToken.None);

        // Act: advance to just before ExpiresOn - skew (30 - 5 = 25 minutes).
        timeProvider.Advance(TimeSpan.FromMinutes(25) - TimeSpan.FromTicks(1));
        var second = await provider.GetTokenAsync(CancellationToken.None);

        // Assert
        second.Should().Be(first, "a fresh cached token is returned as-is");
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // --- (e) Expiry-refresh ---

    [TestMethod]
    public async Task GetTokenAsync_PastSkewBoundary_TriggersExactlyOneNewCall()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Start);
        var credential = CredentialReturning(Start.AddMinutes(30));
        var provider = Provider(credential, ValidCertificateOptions(), timeProvider);
        await provider.GetTokenAsync(CancellationToken.None);

        // Act: advance past ExpiresOn - skew, then call twice more; the second call
        // must hit the newly refreshed cache, not the credential.
        timeProvider.Advance(TimeSpan.FromMinutes(26));
        credential
            .Setup(c =>
                c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new AccessToken(FakeToken, timeProvider.GetUtcNow().AddMinutes(30)));
        await provider.GetTokenAsync(CancellationToken.None);
        await provider.GetTokenAsync(CancellationToken.None);

        // Assert: one initial acquisition + exactly one refresh.
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    // --- (f) Skew boundary exactness ---

    [TestMethod]
    public async Task GetTokenAsync_AtExactlySkewBoundary_RefreshesButOneTickEarlierDoesNot()
    {
        // Arrange: ExpiresOn = Start + 30, skew = 5, boundary = Start + 25.
        var timeProvider = new FakeTimeProvider(Start);
        var credential = CredentialReturning(Start.AddMinutes(30));
        var provider = Provider(credential, ValidCertificateOptions(), timeProvider);
        await provider.GetTokenAsync(CancellationToken.None);

        // Act + Assert: one tick before the boundary — still cached.
        timeProvider.Advance(TimeSpan.FromMinutes(25) - TimeSpan.FromTicks(1));
        await provider.GetTokenAsync(CancellationToken.None);
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Act + Assert: at exactly the boundary — stale, refresh occurs.
        timeProvider.Advance(TimeSpan.FromTicks(1));
        await provider.GetTokenAsync(CancellationToken.None);
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    // --- (g) Default skew ---

    [TestMethod]
    public async Task GetTokenAsync_DefaultOptions_UseFiveMinuteSkew()
    {
        // Arrange: RefreshSkewMinutes is not set, so the D5 default of 5 applies.
        var timeProvider = new FakeTimeProvider(Start);
        var credential = CredentialReturning(Start.AddMinutes(10));
        var options = ValidCertificateOptions();
        var provider = Provider(credential, options, timeProvider);
        await provider.GetTokenAsync(CancellationToken.None);

        // Act + Assert: fresh until Start + (10 - 5) minutes, stale at the boundary.
        timeProvider.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        await provider.GetTokenAsync(CancellationToken.None);
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        timeProvider.Advance(TimeSpan.FromTicks(1));
        await provider.GetTokenAsync(CancellationToken.None);
        credential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }
}
