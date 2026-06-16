using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.MailBridge;

internal class BridgeApplication
{
    private static readonly JsonSerializerOptions SettingsSerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task<int> RunAsync(string[] args)
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

        if (errors.Count > 0)
        {
            Console.Error.WriteLine(string.Join(";", errors));
            return 2;
        }

        using var host = BuildHost(args, settings);
        await RunHostAsync(host);
        return 0;
    }

    internal virtual IHost BuildHost(string[] args, BridgeSettings settings)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(new BridgeStateStore(settings));
        builder.Services.AddSingleton<CacheRepository>();
        builder.Services.AddSingleton<IBridgeRepository>(sp =>
            sp.GetRequiredService<CacheRepository>()
        );
        builder.Services.AddSingleton<IScanStateRepository>(sp =>
            sp.GetRequiredService<CacheRepository>()
        );
        builder.Services.AddSingleton<IOutlookStaExecutor, OutlookStaExecutor>();
        builder.Services.AddSingleton<IOutlookApplicationProvider, OutlookApplicationProvider>();
        builder.Services.AddSingleton<IOutlookMailSender, OutlookComMailSender>();
        builder.Services.AddSingleton<IOutlookScanner, OutlookScanner>();
        builder.Services.AddHostedService<ScanWorker>();
        builder.Services.AddHostedService<PipeRpcWorker>();
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "OpenClaw.MailBridge";
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        return builder.Build();
    }

    internal virtual Task RunHostAsync(IHost host) => host.RunAsync();

    internal BridgeSettings LoadSettings(string path)
    {
        EnsureSettingsDirectory(path);
        if (!SettingsStoreExists(path))
        {
            WriteSettingsStore(
                path,
                JsonSerializer.Serialize(BridgeSettings.Default, SettingsSerializerOptions)
            );
            return BridgeSettings.Default;
        }

        return MergeWithDefaults(ReadSettingsStore(path)) ?? BridgeSettings.Default;
    }

    /// <summary>
    /// Overlays a persisted settings document onto <see cref="BridgeSettings.Default"/> so that
    /// fields absent from an older-schema file retain their default values while fields present
    /// in the file are honored verbatim (including values that later fail validation). Returns
    /// <c>null</c> when the persisted document is JSON <c>null</c>, mirroring the prior
    /// <see cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions)"/> contract.
    /// </summary>
    private static BridgeSettings? MergeWithDefaults(string storedContent)
    {
        if (JsonNode.Parse(storedContent) is not JsonObject storedObject)
        {
            return null;
        }

        var merged = (JsonObject)
            JsonSerializer.SerializeToNode(BridgeSettings.Default, SettingsSerializerOptions)!;

        foreach (var property in storedObject)
        {
            merged[property.Key] = property.Value?.DeepClone();
        }

        return merged.Deserialize<BridgeSettings>(SettingsSerializerOptions);
    }

    internal virtual void EnsureSettingsDirectory(string path) =>
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    internal virtual bool SettingsStoreExists(string path) => File.Exists(path);

    internal virtual void WriteSettingsStore(string path, string content) =>
        File.WriteAllText(path, content);

    internal virtual string ReadSettingsStore(string path) => File.ReadAllText(path);

    internal static string? GetArg(string[] args, string key)
    {
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
