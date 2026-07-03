using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;
using OpenClaw.HostAdapter.Contracts;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Composition-root backend-selection tests (D8, AC-5) through
/// <see cref="CoreTestWebApplicationFactory"/>: with
/// <c>OpenClaw:GraphAdapter:Enabled</c> absent the composition root registers the
/// local <c>HostAdapterHttpClient</c> exactly as today; with <c>Enabled=true</c> plus
/// valid Graph/CloudAuth configuration it resolves <c>GraphHostAdapterClient</c>. No
/// live calls occur: resolution only builds the object graph.
/// </summary>
[TestClass]
public sealed class GraphBackendSelectionTests
{
    [TestMethod]
    public void DefaultPath_GraphAdapterAbsent_ResolvesHostAdapterHttpClient()
    {
        using var factory = new CoreTestWebApplicationFactory(null);

        var client = factory.Services.GetRequiredService<IHostAdapterClient>();

        client
            .Should()
            .BeOfType<HostAdapterHttpClient>(
                "the flag-absent default path keeps the local HTTP client registration"
            );
    }

    [TestMethod]
    public void OptInPath_GraphAdapterEnabled_ResolvesGraphHostAdapterClient()
    {
        // UseSetting (not ConfigureAppConfiguration) so the flag is present in
        // builder.Configuration when Program.cs reads it at composition time — the
        // factory's ConfigureAppConfiguration callbacks run too late for direct
        // configuration reads under minimal hosting.
        using var factory = new CoreTestWebApplicationFactory(null);
        using var graphFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OpenClaw:GraphAdapter:Enabled", "true");
            builder.UseSetting("OpenClaw:GraphAdapter:PrincipalMailboxUpn", "paula@contoso.com");
            builder.UseSetting("OpenClaw:GraphAdapter:AssistantMailboxUpn", "amy@contoso.com");
            builder.UseSetting(
                "OpenClaw:CloudAuth:TenantId",
                "00000000-0000-0000-0000-000000000001"
            );
            builder.UseSetting(
                "OpenClaw:CloudAuth:ClientId",
                "00000000-0000-0000-0000-000000000002"
            );
            builder.UseSetting("OpenClaw:CloudAuth:CertificatePath", "/run/secrets/fake-cert.pem");
        });

        var client = graphFactory.Services.GetRequiredService<IHostAdapterClient>();

        client
            .Should()
            .BeOfType<GraphHostAdapterClient>(
                "OpenClaw:GraphAdapter:Enabled=true selects the Graph backend"
            );
    }
}
