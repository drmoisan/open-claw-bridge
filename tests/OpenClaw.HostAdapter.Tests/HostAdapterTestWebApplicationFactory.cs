using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.HostAdapter.Tests;

internal sealed class HostAdapterTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> additionalConfiguration;

    public HostAdapterTestWebApplicationFactory()
        : this(new Dictionary<string, string?>()) { }

    public HostAdapterTestWebApplicationFactory(
        IReadOnlyDictionary<string, string?> additionalConfiguration
    )
    {
        this.additionalConfiguration = additionalConfiguration;
    }

    public string ExpectedToken { get; } = "expected-hostadapter-token";

    public HostAdapterProcessRunnerStub ProcessRunner { get; } = new();

    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ExpectedToken
        );
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var missingAppSettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "missing-hostadapter-appsettings.json"
        );

        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [$"{HostAdapterOptions.SectionName}:AppSettingsPath"] =
                            missingAppSettingsPath,
                        [$"{HostAdapterOptions.SectionName}:TokenFilePath"] = "unused-in-tests",
                        [$"{HostAdapterOptions.SectionName}:ClientExecutablePath"] =
                            "OpenClaw.MailBridge.Client.exe",
                        [$"{HostAdapterOptions.SectionName}:AdapterVersion"] = "test-version",
                    }
                );

                if (additionalConfiguration.Count > 0)
                {
                    configurationBuilder.AddInMemoryCollection(additionalConfiguration);
                }
            }
        );

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostAdapterProcessRunner>();
            services.RemoveAll<IHostAdapterTokenProvider>();
            services.AddSingleton<IHostAdapterProcessRunner>(ProcessRunner);
            services.AddSingleton<IHostAdapterTokenProvider>(
                new FixedHostAdapterTokenProvider(ExpectedToken)
            );
        });
    }
}

internal sealed class HostAdapterProcessRunnerStub : IHostAdapterProcessRunner
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<object>> responses = new(
        StringComparer.Ordinal
    );
    private readonly List<HostAdapterProcessInvocation> invocations = [];
    private readonly object gate = new();
    private int invocationCount;

    public int InvocationCount => invocationCount;

    public IReadOnlyList<HostAdapterProcessInvocation> Invocations
    {
        get
        {
            lock (gate)
            {
                return invocations.ToArray();
            }
        }
    }

    public void EnqueueResponse<T>(string verb, AdapterCommandResult<T> response)
    {
        var queue = responses.GetOrAdd(verb, static _ => new ConcurrentQueue<object>());
        queue.Enqueue(response);
    }

    public Task<AdapterCommandResult<T>> ExecuteAsync<T>(
        ProcessStartInfo startInfo,
        string requestId,
        BridgeStatusDto? bridge,
        Func<JsonElement, T> projector,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref invocationCount);
        var verb = startInfo.ArgumentList.FirstOrDefault() ?? string.Empty;

        lock (gate)
        {
            invocations.Add(
                new HostAdapterProcessInvocation(
                    startInfo.FileName,
                    verb,
                    startInfo.ArgumentList.ToArray()
                )
            );
        }

        if (
            responses.TryGetValue(verb, out var queue)
            && queue.TryDequeue(out var queuedResponse)
            && queuedResponse is AdapterCommandResult<T> typedResponse
        )
        {
            return Task.FromResult(typedResponse);
        }

        return Task.FromResult(
            HostAdapterResponses.Failure<T>(
                StatusCodes.Status502BadGateway,
                requestId,
                "test-version",
                "UNEXPECTED_INVOCATION",
                $"The CLI runner was unexpectedly invoked for verb '{verb}'.",
                bridge,
                retryable: false,
                cliExitCode: -1
            )
        );
    }
}

internal sealed record HostAdapterProcessInvocation(
    string FileName,
    string Verb,
    IReadOnlyList<string> Arguments
);

internal sealed class FixedHostAdapterTokenProvider(string expectedToken)
    : IHostAdapterTokenProvider
{
    public string? ReadExpectedToken() => expectedToken;
}
