using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudAuth;

namespace OpenClaw.Core.Tests.CloudAuth;

/// <summary>
/// D8 registration behavior of
/// <see cref="CloudAuthServiceCollectionExtensions.AddCloudAuth"/>: in-memory
/// configuration binding of <c>OpenClaw:CloudAuth</c> (including D5 defaults for
/// omitted keys), singleton <see cref="IAppTokenProvider"/> resolution, and
/// fail-closed <c>ValidateOnStart</c> surfacing of invalid configuration. No temp
/// files: all configuration is <c>AddInMemoryCollection</c>.
/// </summary>
[TestClass]
public sealed class CloudAuthServiceCollectionExtensionsTests
{
    private const string FakeTenantId = "00000000-0000-0000-0000-000000000001";
    private const string FakeClientId = "00000000-0000-0000-0000-000000000002";
    private const string FakeCertificatePath = "/run/secrets/fake-cert.pem";
    private const string FakeClientSecret = "fake-client-secret-value";

    private static ServiceProvider BuildProvider(Dictionary<string, string?> configValues)
    {
        // Arrange helper: in-memory configuration plus the TimeProvider and logging
        // dependencies the provider needs — production wiring is untouched.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.Zero))
        );
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddCloudAuth(configuration);
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public void AddCloudAuth_ValidCertificateFirstConfiguration_BindsAllKeysAndDefaults()
    {
        // Arrange: certificate-first configuration omitting Scope, AuthorityHost, and
        // RefreshSkewMinutes so the D5 defaults must apply.
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["OpenClaw:CloudAuth:TenantId"] = FakeTenantId,
                ["OpenClaw:CloudAuth:ClientId"] = FakeClientId,
                ["OpenClaw:CloudAuth:CertificatePath"] = FakeCertificatePath,
            }
        );

        // Act
        var options = provider.GetRequiredService<IOptions<CloudAuthOptions>>().Value;

        // Assert
        options.TenantId.Should().Be(FakeTenantId);
        options.ClientId.Should().Be(FakeClientId);
        options.CertificatePath.Should().Be(FakeCertificatePath);
        options.ClientSecret.Should().BeEmpty();
        options.Scope.Should().Be("https://graph.microsoft.com/.default", "D5 default");
        options.AuthorityHost.Should().Be("https://login.microsoftonline.com/", "D5 default");
        options.RefreshSkewMinutes.Should().Be(5, "D5 default");
    }

    [TestMethod]
    public void AddCloudAuth_ExplicitOptionalKeys_OverrideTheDefaults()
    {
        // Arrange
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["OpenClaw:CloudAuth:TenantId"] = FakeTenantId,
                ["OpenClaw:CloudAuth:ClientId"] = FakeClientId,
                ["OpenClaw:CloudAuth:CertificatePath"] = FakeCertificatePath,
                ["OpenClaw:CloudAuth:Scope"] = "https://graph.microsoft.us/.default",
                ["OpenClaw:CloudAuth:AuthorityHost"] = "https://login.microsoftonline.us/",
                ["OpenClaw:CloudAuth:RefreshSkewMinutes"] = "10",
            }
        );

        // Act
        var options = provider.GetRequiredService<IOptions<CloudAuthOptions>>().Value;

        // Assert
        options.Scope.Should().Be("https://graph.microsoft.us/.default");
        options.AuthorityHost.Should().Be("https://login.microsoftonline.us/");
        options.RefreshSkewMinutes.Should().Be(10);
    }

    [TestMethod]
    public void AddCloudAuth_IAppTokenProvider_ResolvesAsSingletonClientCredentialsTokenProvider()
    {
        // Arrange
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["OpenClaw:CloudAuth:TenantId"] = FakeTenantId,
                ["OpenClaw:CloudAuth:ClientId"] = FakeClientId,
                ["OpenClaw:CloudAuth:CertificatePath"] = FakeCertificatePath,
            }
        );

        // Act
        var first = provider.GetRequiredService<IAppTokenProvider>();
        var second = provider.GetRequiredService<IAppTokenProvider>();

        // Assert
        first
            .Should()
            .BeOfType<ClientCredentialsTokenProvider>(
                "AddCloudAuth registers the Azure.Identity-backed implementation"
            );
        second.Should().BeSameAs(first, "the provider is a singleton");
    }

    [TestMethod]
    public void AddCloudAuth_InvalidConfiguration_FailsClosedWithOptionsValidationException()
    {
        // Arrange: both credential sources set — the reject-ambiguous D5 violation.
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["OpenClaw:CloudAuth:TenantId"] = FakeTenantId,
                ["OpenClaw:CloudAuth:ClientId"] = FakeClientId,
                ["OpenClaw:CloudAuth:CertificatePath"] = FakeCertificatePath,
                ["OpenClaw:CloudAuth:ClientSecret"] = FakeClientSecret,
            }
        );

        // Act: options access is where ValidateOnStart semantics surface in a manually
        // built container (the host runner invokes the same validation at startup).
        var act = () => provider.GetRequiredService<IAppTokenProvider>();

        // Assert: fail-closed with the validator-delegating failure message; no
        // configured value is echoed.
        act.Should()
            .Throw<OptionsValidationException>()
            .Which.Message.Should()
            .Contain("CloudAuth options are invalid")
            .And.NotContain(FakeClientSecret)
            .And.NotContain(FakeCertificatePath);
    }
}
