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
        FormatAdapterVersion(typeof(HostAdapterOptions).Assembly.GetName().Version);

    private static string FormatAdapterVersion(Version? version)
    {
        // The assembly version renders four components (major.minor.build.revision); the adapter
        // version reported in meta.adapterVersion uses the three-component major.minor.patch form
        // (for example 1.0.0). A missing version falls back to the historical 0.1.0 value.
        if (version is null)
        {
            return "0.1.0";
        }

        var build = version.Build < 0 ? 0 : version.Build;
        return $"{version.Major}.{version.Minor}.{build}";
    }

    public string AppSettingsPath { get; set; } = DefaultAppSettingsPath;

    public string TokenFilePath { get; set; } = DefaultTokenFilePath;

    public string ClientExecutablePath { get; set; } = DefaultClientExecutablePath;

    public int DefaultLimit { get; set; } = 100;

    public int MaxLimit { get; set; } = 250;

    public string AdapterVersion { get; set; } = DefaultAdapterVersion;

    /// <summary>
    /// The mailbox identifier rendered into the Graph-shaped <c>/users/{id}/...</c> route segment.
    /// Defaults to the non-identifying literal <c>"me"</c>.
    /// </summary>
    public string MailboxId { get; set; } = "me";

    /// <summary>
    /// The mailbox time zone and working hours served by
    /// <c>GET /users/{id}/mailboxSettings</c>, bound from the
    /// <c>OpenClaw:HostAdapter:MailboxSettings</c> subsection. Defaults to UTC, Monday–Friday,
    /// 09:00–17:00 when the subsection is absent.
    /// </summary>
    public MailboxSettingsOptions MailboxSettings { get; set; } = new();
}
