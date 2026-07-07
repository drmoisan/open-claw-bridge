using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// D6 registration behavior of
/// <see cref="ScopeValidationServiceCollectionExtensions.AddScopeBoundaryValidation"/>,
/// patterned on <c>GraphServiceCollectionExtensionsTests</c>. Covers all three branches
/// (disabled/absent registers nothing; enabled without the Graph adapter throws at
/// composition time; enabled with the Graph adapter resolves all services and binds
/// options), the <c>ValidateOnStart</c> options-validation surface, and null-argument
/// guards. No temp files: configuration is <c>AddInMemoryCollection</c>.
/// </summary>
[TestClass]
public sealed class ScopeValidationServiceCollectionExtensionsTests
{
    private const string FakeTenantId = "00000000-0000-0000-0000-000000000001";
    private const string FakeClientId = "00000000-0000-0000-0000-000000000002";
    private const string FakeCertificatePath = "/run/secrets/fake-cert.pem";

    private static Dictionary<string, string?> EnabledConfiguration() =>
        new()
        {
            ["OpenClaw:ScopeValidation:Enabled"] = "true",
            ["OpenClaw:ScopeValidation:InScopeTestMailboxUpn"] = "in-scope@contoso.com",
            ["OpenClaw:ScopeValidation:OutOfScopeTestMailboxUpn"] = "out-of-scope@contoso.com",
            ["OpenClaw:GraphAdapter:Enabled"] = "true",
            ["OpenClaw:GraphAdapter:PrincipalMailboxUpn"] = "paula@contoso.com",
            ["OpenClaw:GraphAdapter:AssistantMailboxUpn"] = "amy@contoso.com",
            ["OpenClaw:CloudAuth:TenantId"] = FakeTenantId,
            ["OpenClaw:CloudAuth:ClientId"] = FakeClientId,
            ["OpenClaw:CloudAuth:CertificatePath"] = FakeCertificatePath,
        };

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider BuildGraphEnabledProvider(Dictionary<string, string?> values)
    {
        var configuration = BuildConfiguration(values);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero))
        );
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddGraphHostAdapterClient(configuration);
        services.AddScopeBoundaryValidation(configuration);
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public void AddScopeBoundaryValidation_Disabled_RegistersNothing()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?> { ["OpenClaw:ScopeValidation:Enabled"] = "false" }
        );
        var services = new ServiceCollection();

        services.AddScopeBoundaryValidation(configuration);

        services
            .Should()
            .NotContain(d => d.ServiceType == typeof(IMailboxScopeProbe))
            .And.NotContain(d => d.ServiceType == typeof(ScopeBoundaryValidator));
        services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Should()
            .NotContain(d => d.ImplementationType == typeof(ScopeBoundaryStartupValidator));
    }

    [TestMethod]
    public void AddScopeBoundaryValidation_AbsentSection_RegistersNothing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = new ServiceCollection();

        services.AddScopeBoundaryValidation(configuration);

        services.Should().NotContain(d => d.ServiceType == typeof(IMailboxScopeProbe));
    }

    [TestMethod]
    public void AddScopeBoundaryValidation_EnabledWithoutGraphAdapter_ThrowsAtCompositionTime()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["OpenClaw:ScopeValidation:Enabled"] = "true",
                ["OpenClaw:ScopeValidation:InScopeTestMailboxUpn"] = "in-scope@contoso.com",
                ["OpenClaw:ScopeValidation:OutOfScopeTestMailboxUpn"] = "out-of-scope@contoso.com",
                ["OpenClaw:GraphAdapter:Enabled"] = "false",
            }
        );

        var act = () => new ServiceCollection().AddScopeBoundaryValidation(configuration);

        act.Should()
            .Throw<InvalidOperationException>()
            .Which.Message.Should()
            .Contain("GraphAdapter");
    }

    [TestMethod]
    public void AddScopeBoundaryValidation_EnabledWithGraph_ResolvesAllServices()
    {
        using var provider = BuildGraphEnabledProvider(EnabledConfiguration());

        provider
            .GetRequiredService<IMailboxScopeProbe>()
            .Should()
            .BeOfType<GraphMailboxScopeProbe>();
        provider.GetRequiredService<ScopeBoundaryValidator>().Should().NotBeNull();
        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(s => s is ScopeBoundaryStartupValidator);
    }

    [TestMethod]
    public void AddScopeBoundaryValidation_EnabledWithGraph_BindsOptionsFromConfigurationKeys()
    {
        using var provider = BuildGraphEnabledProvider(EnabledConfiguration());

        var options = provider.GetRequiredService<IOptions<ScopeValidationOptions>>().Value;

        options.Enabled.Should().BeTrue();
        options.InScopeTestMailboxUpn.Should().Be("in-scope@contoso.com");
        options.OutOfScopeTestMailboxUpn.Should().Be("out-of-scope@contoso.com");
    }

    [DataTestMethod]
    [DataRow(
        "OpenClaw:ScopeValidation:InScopeTestMailboxUpn",
        "",
        DisplayName = "missing in-scope UPN"
    )]
    [DataRow(
        "OpenClaw:ScopeValidation:OutOfScopeTestMailboxUpn",
        "in-scope@contoso.com",
        DisplayName = "equal UPNs"
    )]
    public void AddScopeBoundaryValidation_InvalidOptions_FailClosedAtValidation(
        string key,
        string value
    )
    {
        var config = EnabledConfiguration();
        config[key] = value;
        using var provider = BuildGraphEnabledProvider(config);

        var act = () => provider.GetRequiredService<IOptions<ScopeValidationOptions>>().Value;

        act.Should()
            .Throw<OptionsValidationException>()
            .Which.Message.Should()
            .Contain("ScopeValidation options are invalid");
    }

    [TestMethod]
    public void AddScopeBoundaryValidation_NullServices_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        var act = () =>
            ScopeValidationServiceCollectionExtensions.AddScopeBoundaryValidation(
                null!,
                configuration
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void AddScopeBoundaryValidation_NullConfiguration_Throws()
    {
        var act = () => new ServiceCollection().AddScopeBoundaryValidation(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
