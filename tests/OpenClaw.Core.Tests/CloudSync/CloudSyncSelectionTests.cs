using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudSync;

namespace OpenClaw.Core.Tests.CloudSync;

/// <summary>
/// Composition-root opt-in tests for CloudSync (D-6, AC-4) through
/// <see cref="CoreTestWebApplicationFactory"/> in the <c>GraphBackendSelectionTests</c>
/// style: with the flag absent, <c>POST /graph/notifications</c> is not mapped (404)
/// and no CloudSync hosted services are registered — the composition root is
/// unchanged; with <c>OpenClaw:CloudSync:Enabled=true</c> plus valid
/// Graph/CloudAuth/CloudSync settings, the route is mapped (handshake 200) and the
/// three CloudSync workers are registered. <c>UseSetting</c> is required because
/// Program.cs reads the flag at composition time under minimal hosting.
/// </summary>
[TestClass]
public sealed class CloudSyncSelectionTests
{
    private static WebApplicationFactoryFixture OptInFactory(
        CoreTestWebApplicationFactory factory
    ) =>
        new(
            factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("OpenClaw:GraphAdapter:Enabled", "true");
                builder.UseSetting(
                    "OpenClaw:GraphAdapter:PrincipalMailboxUpn",
                    "paula@contoso.com"
                );
                builder.UseSetting("OpenClaw:GraphAdapter:AssistantMailboxUpn", "amy@contoso.com");
                builder.UseSetting(
                    "OpenClaw:CloudAuth:TenantId",
                    "00000000-0000-0000-0000-000000000001"
                );
                builder.UseSetting(
                    "OpenClaw:CloudAuth:ClientId",
                    "00000000-0000-0000-0000-000000000002"
                );
                builder.UseSetting(
                    "OpenClaw:CloudAuth:CertificatePath",
                    "/run/secrets/fake-cert.pem"
                );
                builder.UseSetting("OpenClaw:CloudSync:Enabled", "true");
                builder.UseSetting(
                    "OpenClaw:CloudSync:NotificationUrl",
                    "https://webhook.contoso.com/graph/notifications"
                );
            })
        );

    /// <summary>Disposal wrapper so the derived factory is always cleaned up.</summary>
    private sealed class WebApplicationFactoryFixture(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory
    ) : IDisposable
    {
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory { get; } =
            factory;

        public void Dispose() => Factory.Dispose();
    }

    [TestMethod]
    public async Task FlagAbsent_NotificationsRouteIsNotMappedAndNoCloudSyncWorkersRun()
    {
        // Arrange
        using var factory = new CoreTestWebApplicationFactory(null);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/graph/notifications?validationToken=abc", null);
        var hostedServices = factory.Services.GetServices<IHostedService>().ToList();

        // Assert
        response
            .StatusCode.Should()
            .Be(
                HttpStatusCode.NotFound,
                "the flag-absent composition root maps no CloudSync route"
            );
        hostedServices
            .Should()
            .NotContain(
                s =>
                    s is SubscriptionRenewalWorker
                    || s is DeltaReconciliationWorker
                    || s is NotificationDispatchWorker,
                "the flag-absent composition root registers no CloudSync workers"
            );
    }

    [TestMethod]
    public async Task OptIn_MapsTheRouteAndRegistersTheThreeWorkers()
    {
        // Arrange
        using var baseFactory = new CoreTestWebApplicationFactory(null);
        using var fixture = OptInFactory(baseFactory);
        using var client = fixture.Factory.CreateClient();

        // Act
        var response = await client.PostAsync("/graph/notifications?validationToken=ping", null);
        var hostedServices = fixture.Factory.Services.GetServices<IHostedService>().ToList();

        // Assert
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "the opt-in composition root maps the webhook handshake");
        (await response.Content.ReadAsStringAsync()).Should().Be("ping");
        hostedServices.Should().ContainSingle(s => s is SubscriptionRenewalWorker);
        hostedServices.Should().ContainSingle(s => s is DeltaReconciliationWorker);
        hostedServices.Should().ContainSingle(s => s is NotificationDispatchWorker);
    }
}
