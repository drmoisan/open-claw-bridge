using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudAuth;
using OpenClaw.Core.CloudGraph;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// D8 registration behavior of
/// <see cref="GraphServiceCollectionExtensions.AddGraphHostAdapterClient"/>, patterned
/// on the CloudAuth extension tests: resolving <see cref="IHostAdapterClient"/> from a
/// valid enabled configuration yields <c>GraphHostAdapterClient</c>, options bind from
/// configuration keys, <see cref="IAppTokenProvider"/> is registered (AddCloudAuth is
/// invoked internally), invalid options fail closed at options validation, and
/// null-argument guards throw. No temp files: all configuration is
/// <c>AddInMemoryCollection</c>.
/// </summary>
[TestClass]
public sealed class GraphServiceCollectionExtensionsTests
{
    private const string FakeTenantId = "00000000-0000-0000-0000-000000000001";
    private const string FakeClientId = "00000000-0000-0000-0000-000000000002";
    private const string FakeCertificatePath = "/run/secrets/fake-cert.pem";

    private static Dictionary<string, string?> ValidConfiguration() =>
        new()
        {
            ["OpenClaw:GraphAdapter:Enabled"] = "true",
            ["OpenClaw:GraphAdapter:PrincipalMailboxUpn"] = "paula@contoso.com",
            ["OpenClaw:GraphAdapter:AssistantMailboxUpn"] = "amy@contoso.com",
            ["OpenClaw:CloudAuth:TenantId"] = FakeTenantId,
            ["OpenClaw:CloudAuth:ClientId"] = FakeClientId,
            ["OpenClaw:CloudAuth:CertificatePath"] = FakeCertificatePath,
        };

    private static ServiceProvider BuildProvider(Dictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero))
        );
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGraphHostAdapterClient(configuration);
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public void AddGraphHostAdapterClient_ValidEnabledConfiguration_ResolvesGraphClient()
    {
        using var provider = BuildProvider(ValidConfiguration());

        var client = provider.GetRequiredService<IHostAdapterClient>();

        client
            .Should()
            .BeOfType<GraphHostAdapterClient>(
                "the typed-client registration binds the interface to the Graph backend"
            );
    }

    [TestMethod]
    public void AddGraphHostAdapterClient_BindsOptionsFromConfigurationKeys()
    {
        var config = ValidConfiguration();
        config["OpenClaw:GraphAdapter:PageSize"] = "25";
        config["OpenClaw:GraphAdapter:MaxPages"] = "3";
        config["OpenClaw:GraphAdapter:PreferredTimeZone"] = "Pacific Standard Time";
        using var provider = BuildProvider(config);

        var options = provider.GetRequiredService<IOptions<GraphAdapterOptions>>().Value;

        options.Enabled.Should().BeTrue();
        options.PrincipalMailboxUpn.Should().Be("paula@contoso.com");
        options.AssistantMailboxUpn.Should().Be("amy@contoso.com");
        options.PageSize.Should().Be(25);
        options.MaxPages.Should().Be(3);
        options.PreferredTimeZone.Should().Be("Pacific Standard Time");
        options.BaseUrl.Should().Be("https://graph.microsoft.com/v1.0/", "spec default");
        options.MaxAttempts.Should().Be(4, "spec default");
        options.BaseDelaySeconds.Should().Be(1, "spec default");
        options.MaxDelaySeconds.Should().Be(30, "spec default");
        options.AvailabilityViewIntervalMinutes.Should().Be(30, "spec default");
    }

    [TestMethod]
    public void AddGraphHostAdapterClient_RegistersIAppTokenProviderViaAddCloudAuth()
    {
        using var provider = BuildProvider(ValidConfiguration());

        var tokenProvider = provider.GetRequiredService<IAppTokenProvider>();

        tokenProvider.Should().NotBeNull("AddGraphHostAdapterClient internally calls AddCloudAuth");
    }

    [DataTestMethod]
    [DataRow(
        "OpenClaw:GraphAdapter:PrincipalMailboxUpn",
        "",
        DisplayName = "missing principal UPN"
    )]
    [DataRow(
        "OpenClaw:GraphAdapter:BaseUrl",
        "http://graph.example.test/v1.0/",
        DisplayName = "http BaseUrl"
    )]
    public void AddGraphHostAdapterClient_InvalidOptions_FailClosedAtValidation(
        string key,
        string value
    )
    {
        var config = ValidConfiguration();
        config[key] = value;
        using var provider = BuildProvider(config);

        var act = () => provider.GetRequiredService<IOptions<GraphAdapterOptions>>().Value;

        act.Should()
            .Throw<OptionsValidationException>()
            .Which.Message.Should()
            .Contain("GraphAdapter options are invalid");
    }

    [TestMethod]
    public void AddGraphHostAdapterClient_BindsIndexedAllowlistKeysToTheCollection()
    {
        var config = ValidConfiguration();
        config["OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns:0"] = "exec-one@contoso.com";
        config["OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns:1"] = "exec-two@contoso.com";
        using var provider = BuildProvider(config);

        var options = provider.GetRequiredService<IOptions<GraphAdapterOptions>>().Value;

        options
            .AllowedPrincipalMailboxUpns.Should()
            .Equal("exec-one@contoso.com", "exec-two@contoso.com");
    }

    [TestMethod]
    public void AddGraphHostAdapterClient_WhitespaceAllowlistEntry_FailsValidateOnStart()
    {
        var config = ValidConfiguration();
        config["OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns:0"] = "exec-one@contoso.com";
        config["OpenClaw:GraphAdapter:AllowedPrincipalMailboxUpns:1"] = "   ";
        using var provider = BuildProvider(config);

        var act = () => provider.GetRequiredService<IOptions<GraphAdapterOptions>>().Value;

        act.Should()
            .Throw<OptionsValidationException>("a whitespace-only allowlist entry fails startup")
            .Which.Message.Should()
            .Contain("GraphAdapter options are invalid");
    }

    [TestMethod]
    public void AddGraphHostAdapterClient_NullServices_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        var act = () =>
            GraphServiceCollectionExtensions.AddGraphHostAdapterClient(null!, configuration);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void AddGraphHostAdapterClient_NullConfiguration_Throws()
    {
        var act = () => new ServiceCollection().AddGraphHostAdapterClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
