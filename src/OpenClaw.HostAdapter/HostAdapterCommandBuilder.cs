using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenClaw.HostAdapter.Contracts;

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

    public ProcessStartInfo BuildGetEventForMessage(string bridgeId)
    {
        var startInfo = CreateBaseStartInfo("get-event-for-message");
        AddOption(startInfo, "id", bridgeId);
        return startInfo;
    }

    private static readonly JsonSerializerOptions RecipientJsonOptions = new(
        JsonSerializerDefaults.Web
    );

    /// <summary>
    /// Builds the <c>send-mail</c> CLI invocation with flat <c>--key value</c> options. Recipient
    /// lists are JSON-serialized arrays of <c>{address, name}</c> per recipient type (D-C);
    /// <c>--save-to-sent-items</c> defaults to <c>true</c> when the request omits it (AC-08).
    /// </summary>
    public ProcessStartInfo BuildSendMail(SendMailRequest request)
    {
        var message = request.Message;
        var startInfo = CreateBaseStartInfo("send-mail");
        AddOption(startInfo, "subject", message.Subject);
        AddOption(startInfo, "body-content-type", message.Body.ContentType);
        AddOption(startInfo, "body-content", message.Body.Content);
        AddOption(startInfo, "to-recipients", SerializeRecipients(message.ToRecipients));
        AddOption(startInfo, "cc-recipients", SerializeRecipients(message.CcRecipients));
        AddOption(startInfo, "bcc-recipients", SerializeRecipients(message.BccRecipients));
        AddOption(startInfo, "save-to-sent-items", request.SaveToSentItems ? "true" : "false");
        return startInfo;
    }

    private static string SerializeRecipients(IReadOnlyList<SendMailRecipientDto>? recipients) =>
        JsonSerializer.Serialize(recipients ?? [], RecipientJsonOptions);

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
