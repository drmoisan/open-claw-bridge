namespace OpenClaw.HostAdapter;

/// <summary>
/// Holds HostAdapter configuration required to locate credentials, invoke the CLI bridge client,
/// and enforce request defaults during startup and request execution.
/// </summary>
public sealed class HostAdapterOptions
{
    public const string SectionName = "OpenClaw:HostAdapter";

    public static string DefaultAppSettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "OpenClaw",
            "HostAdapter",
            "appsettings.json"
        );

    public static string DefaultTokenFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "OpenClaw",
            "HostAdapter",
            "adapter.token"
        );

    public static string DefaultClientExecutablePath => "OpenClaw.MailBridge.Client.exe";

    public static string DefaultAdapterVersion =>
        typeof(HostAdapterOptions).Assembly.GetName().Version?.ToString() ?? "0.1.0";

    public string AppSettingsPath { get; set; } = DefaultAppSettingsPath;

    public string TokenFilePath { get; set; } = DefaultTokenFilePath;

    public string ClientExecutablePath { get; set; } = DefaultClientExecutablePath;

    public int DefaultLimit { get; set; } = 100;

    public int MaxLimit { get; set; } = 250;

    public string AdapterVersion { get; set; } = DefaultAdapterVersion;
}
