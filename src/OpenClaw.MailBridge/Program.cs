using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

/// <summary>
/// Boots the mail bridge host, loads persisted settings, and wires the background services.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the bridge process and keeps it running until the host shuts down.
    /// </summary>
    /// <param name="args">Command-line arguments used to override the settings file path.</param>
    /// <returns>
    /// A process exit code. Returns <c>0</c> when the host runs normally and <c>2</c> when
    /// settings validation fails before startup.
    /// </returns>
    public static async Task<int> Main(string[] args)
    {
        var configPath =
            GetArg(args, "--config")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenClaw",
                "MailBridge",
                "bridge.settings.json"
            );
        var settings = LoadSettings(configPath);
        var errors = BridgeSettingsValidator.Validate(settings);

        // Fail before the host starts so operators see configuration problems immediately.
        if (errors.Count > 0)
        {
            Console.Error.WriteLine(string.Join(";", errors));
            return 2;
        }

        var builder = Host.CreateApplicationBuilder(args);

        // Register long-lived bridge components once so background services can share state safely.
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(new BridgeStateStore(settings));
        builder.Services.AddSingleton<CacheRepository>();
        builder.Services.AddSingleton<OutlookStaExecutor>();
        builder.Services.AddSingleton<OutlookScanner>();
        builder.Services.AddHostedService<ScanWorker>();
        builder.Services.AddHostedService<PipeRpcWorker>();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        using var host = builder.Build();
        await host.RunAsync();
        return 0;
    }

    /// <summary>
    /// Loads bridge settings from disk, creating a default settings file on first launch.
    /// </summary>
    /// <param name="path">Absolute path to the JSON settings file.</param>
    /// <returns>The deserialized settings payload or the default settings when no file exists.</returns>
    private static BridgeSettings LoadSettings(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Seed first-run installs with a concrete file so operators can edit the effective defaults.
        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    BridgeSettings.Default,
                    new JsonSerializerOptions { WriteIndented = true }
                )
            );
            return BridgeSettings.Default;
        }

        return JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path))
            ?? BridgeSettings.Default;
    }

    /// <summary>
    /// Retrieves the value that follows a named command-line switch.
    /// </summary>
    /// <param name="args">Full command-line argument list.</param>
    /// <param name="key">Switch name to look for.</param>
    /// <returns>The value that follows <paramref name="key"/>, or <see langword="null"/> when absent.</returns>
    private static string? GetArg(string[] args, string key)
    {
        // Walk only to the penultimate argument because every supported switch expects a following value.
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
