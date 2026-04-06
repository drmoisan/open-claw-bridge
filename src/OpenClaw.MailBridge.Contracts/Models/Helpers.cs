using System.Text;
using System.Text.RegularExpressions;

namespace OpenClaw.MailBridge.Contracts.Models;

public static class BridgeIdCodec
{
    public static string MessageId(string entryId, bool isMeeting) =>
        $"{(isMeeting ? "mtg" : "msg")}:{B64(entryId)}";

    public static string EventId(
        string? globalAppointmentId,
        string entryId,
        DateTimeOffset startUtc
    ) =>
        $"evt:{B64(string.IsNullOrWhiteSpace(globalAppointmentId) ? entryId : globalAppointmentId)}:{startUtc.UtcDateTime:O}";

    private static string B64(string value) =>
        Convert
            .ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

public static class BodySanitizer
{
    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex FilePath = new(@"[A-Za-z]:\\[^\s]+", RegexOptions.Compiled);

    public static string NormalizePreview(string? input, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        var noHtml = Tags.Replace(input, " ");
        var noPath = FilePath.Replace(noHtml, "[path]");
        var squashed = Regex.Replace(noPath, "\\s+", " ").Trim();
        return squashed.Length <= maxChars ? squashed : squashed[..maxChars];
    }
}

public static class BridgeSettingsValidator
{
    public static IReadOnlyList<string> Validate(BridgeSettings settings)
    {
        var errors = new List<string>();
        if (
            !string.Equals(settings.Mode, "safe", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(settings.Mode, "enhanced", StringComparison.OrdinalIgnoreCase)
        )
            errors.Add("mode must be safe|enhanced");
        if (settings.InboxPollSeconds < 5)
            errors.Add("inboxPollSeconds must be >= 5");
        if (settings.CalendarPollSeconds < 30)
            errors.Add("calendarPollSeconds must be >= 30");
        if (settings.MaxItemsPerScan is < 1 or > 2000)
            errors.Add("maxItemsPerScan must be 1..2000");
        if (settings.BodyPreviewMaxChars is < 1 or > 2000)
            errors.Add("bodyPreviewMaxChars must be 1..2000");
        if (string.IsNullOrWhiteSpace(settings.PipeName))
            errors.Add("pipeName required");
        return errors;
    }
}
