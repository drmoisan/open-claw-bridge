using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenClaw.Core.Tests;

/// <summary>
/// Regression coverage for issue #137: the <c>PostConfigure</c> blank-config fallback in
/// <c>Program.cs</c> (lines 16-18) must resolve <see cref="OpenClawOptions.HostAdapter"/>'s
/// <c>BaseUrl</c> to HostAdapter's actual root-scoped route surface (no <c>/v1</c> segment)
/// when the bound configuration value is blank. This is a new sibling file to
/// <see cref="HostAdapterHttpClientTests"/>, which is already at the 500-line cap and must
/// not be extended (see plan's Explicit Non-Goals).
/// </summary>
[TestClass]
public sealed class CoreHostAdapterBaseUrlFallbackTests
{
    [TestMethod]
    public void BlankHostAdapterBaseUrl_ResolvesFallbackWithNoV1Segment()
    {
        // Arrange: override OpenClaw:HostAdapter:BaseUrl to an empty string so the
        // Program.cs PostConfigure blank-config fallback branch is exercised.
        using var factory = new CoreTestWebApplicationFactory(null);
        using var blankBaseUrlFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(
                        new Dictionary<string, string?> { ["OpenClaw:HostAdapter:BaseUrl"] = "" }
                    );
                }
            );
        });

        // Act
        var options = blankBaseUrlFactory.Services.GetRequiredService<IOptions<OpenClawOptions>>();
        var resolvedBaseUrl = options.Value.HostAdapter.BaseUrl;

        // Assert
        resolvedBaseUrl.Should().NotContain("/v1");
    }
}
