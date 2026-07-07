using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;
using OpenClaw.Core.CloudGraph;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Registration behavior of
/// <see cref="CloudSyncServiceCollectionExtensions.AddCloudSync"/> (patterned on the
/// Graph extension tests): with valid CloudSync + Graph/CloudAuth configuration every
/// CloudSync service resolves and the three hosted workers are registered; with
/// <c>CloudSync:Enabled=true</c> and <c>GraphAdapter:Enabled</c> absent, options
/// validation fails closed (D-7); a non-https <c>NotificationUrl</c> also fails
/// closed. No temp files: all configuration is <c>AddInMemoryCollection</c>.
/// </summary>
[TestClass]
public sealed class CloudSyncServiceCollectionExtensionsTests
{
    private static Dictionary<string, string?> ValidConfiguration() =>
        new()
        {
            ["OpenClaw:GraphAdapter:Enabled"] = "true",
            ["OpenClaw:GraphAdapter:PrincipalMailboxUpn"] = "paula@contoso.com",
            ["OpenClaw:GraphAdapter:AssistantMailboxUpn"] = "amy@contoso.com",
            ["OpenClaw:CloudAuth:TenantId"] = "00000000-0000-0000-0000-000000000001",
            ["OpenClaw:CloudAuth:ClientId"] = "00000000-0000-0000-0000-000000000002",
            ["OpenClaw:CloudAuth:CertificatePath"] = "/run/secrets/fake-cert.pem",
            ["OpenClaw:CloudSync:Enabled"] = "true",
            ["OpenClaw:CloudSync:NotificationUrl"] =
                "https://webhook.contoso.com/graph/notifications",
        };

    private static ServiceProvider BuildProvider(Dictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero))
        );
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IActionAuditLog>(new FakeActionAuditLog());
        services.AddSingleton(
            new OpenClaw.Core.CoreCacheRepository(
                $"Data Source=core-di-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
            )
        );
        services.AddGraphHostAdapterClient(configuration);
        services.AddCloudSync(configuration);
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public void AddCloudSync_ValidConfiguration_ResolvesAllCloudSyncServices()
    {
        // Arrange
        using var provider = BuildProvider(ValidConfiguration());

        // Act / Assert: resolution only builds the object graph; no live calls occur.
        provider
            .GetRequiredService<INotificationQueue>()
            .Should()
            .BeOfType<ChannelNotificationQueue>();
        provider
            .GetRequiredService<IClientStateGenerator>()
            .Should()
            .BeOfType<CryptoClientStateGenerator>();
        provider
            .GetRequiredService<ISubscriptionStore>()
            .Should()
            .BeSameAs(
                provider.GetRequiredService<OpenClaw.Core.CoreCacheRepository>(),
                "the repository singleton backs the subscription store"
            );
        provider
            .GetRequiredService<IDeltaLinkStore>()
            .Should()
            .BeSameAs(provider.GetRequiredService<OpenClaw.Core.CoreCacheRepository>());
        provider.GetRequiredService<NotificationRequestProcessor>().Should().NotBeNull();
        provider.GetRequiredService<GraphSubscriptionManager>().Should().NotBeNull();
        provider.GetRequiredService<GraphDeltaReconciler>().Should().NotBeNull();
        provider
            .GetRequiredService<IDeltaReconcileTrigger>()
            .Should()
            .BeOfType<GraphDeltaReconciler>("the reconciler satisfies the missed-lifecycle seam");
    }

    [TestMethod]
    public void AddCloudSync_ValidConfiguration_RegistersTheThreeHostedWorkers()
    {
        // Arrange
        using var provider = BuildProvider(ValidConfiguration());

        // Act
        var hostedServices = provider.GetServices<IHostedService>().ToList();

        // Assert
        hostedServices.Should().ContainSingle(s => s is SubscriptionRenewalWorker);
        hostedServices.Should().ContainSingle(s => s is DeltaReconciliationWorker);
        hostedServices.Should().ContainSingle(s => s is NotificationDispatchWorker);
    }

    [TestMethod]
    public void AddCloudSync_GraphBackendAbsent_FailsClosedAtValidation()
    {
        // Arrange: CloudSync enabled while OpenClaw:GraphAdapter:Enabled is absent (D-7).
        var config = ValidConfiguration();
        config.Remove("OpenClaw:GraphAdapter:Enabled");
        using var provider = BuildProvider(config);

        // Act
        var act = () => provider.GetRequiredService<IOptions<CloudSyncOptions>>().Value;

        // Assert
        act.Should()
            .Throw<OptionsValidationException>()
            .Which.Message.Should()
            .Contain("CloudSync options are invalid");
    }

    [TestMethod]
    public void AddCloudSync_HttpNotificationUrl_FailsClosedAtValidation()
    {
        // Arrange
        var config = ValidConfiguration();
        config["OpenClaw:CloudSync:NotificationUrl"] = "http://webhook.contoso.com/graph";
        using var provider = BuildProvider(config);

        // Act
        var act = () => provider.GetRequiredService<IOptions<CloudSyncOptions>>().Value;

        // Assert
        act.Should()
            .Throw<OptionsValidationException>()
            .Which.Message.Should()
            .Contain("CloudSync options are invalid");
    }

    [TestMethod]
    public void AddCloudSync_NullArguments_Throw()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var nullServices = () =>
            CloudSyncServiceCollectionExtensions.AddCloudSync(null!, configuration);
        var nullConfiguration = () => new ServiceCollection().AddCloudSync(null!);

        // Assert
        nullServices.Should().Throw<ArgumentNullException>();
        nullConfiguration.Should().Throw<ArgumentNullException>();
    }
}
