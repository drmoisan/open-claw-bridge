using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace OpenClaw.HostAdapter;

internal sealed class HostAdapterCommandBuilder(IOptions<HostAdapterOptions> optionsAccessor)
{
    private readonly HostAdapterOptions options = optionsAccessor.Value;

    public ProcessStartInfo BuildStatus() => CreateBaseStartInfo("status");

    public ProcessStartInfo BuildListMessages(DateTimeOffset sinceUtc, int limit)
    {
        var startInfo = CreateBaseStartInfo("list-messages");
        AddOption(startInfo, "since", sinceUtc.ToString("O", CultureInfo.InvariantCulture));
        AddOption(startInfo, "limit", limit.ToString(CultureInfo.InvariantCulture));
        return startInfo;
    }

    public ProcessStartInfo BuildGetMessage(string bridgeId)
    {
        var startInfo = CreateBaseStartInfo("get-message");
        AddOption(startInfo, "id", bridgeId);
        return startInfo;
    }

    public ProcessStartInfo BuildListMeetingRequests(DateTimeOffset sinceUtc, int limit)
    {
        var startInfo = CreateBaseStartInfo("list-meeting-requests");
        AddOption(startInfo, "since", sinceUtc.ToString("O", CultureInfo.InvariantCulture));
        AddOption(startInfo, "limit", limit.ToString(CultureInfo.InvariantCulture));
        return startInfo;
    }

    public ProcessStartInfo BuildListCalendar(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int limit
    )
    {
        var startInfo = CreateBaseStartInfo("list-calendar");
        AddOption(startInfo, "start", startUtc.ToString("O", CultureInfo.InvariantCulture));
        AddOption(startInfo, "end", endUtc.ToString("O", CultureInfo.InvariantCulture));
        AddOption(startInfo, "limit", limit.ToString(CultureInfo.InvariantCulture));
        return startInfo;
    }

    public ProcessStartInfo BuildGetEvent(string bridgeId)
    {
        var startInfo = CreateBaseStartInfo("get-event");
        AddOption(startInfo, "id", bridgeId);
        return startInfo;
    }

    private ProcessStartInfo CreateBaseStartInfo(string verb)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };

        var clientPath = options.ClientExecutablePath;
        if (clientPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(clientPath);
        }
        else
        {
            startInfo.FileName = clientPath;
        }

        startInfo.ArgumentList.Add(verb);
        return startInfo;
    }

    private static void AddOption(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add($"--{name}");
        startInfo.ArgumentList.Add(value);
    }
}
