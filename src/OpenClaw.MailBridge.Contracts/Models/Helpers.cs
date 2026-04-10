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

    public static bool TryDecodeMessageId(string? bridgeId, out string entryId, out bool isMeeting)
    {
        entryId = string.Empty;
        isMeeting = false;

        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            return false;
        }

        var parts = bridgeId.Split(':', 2, StringSplitOptions.None);
        if (parts.Length != 2 || (parts[0] != "msg" && parts[0] != "mtg"))
        {
            return false;
        }

        if (!TryDecode(parts[1], out entryId))
        {
            return false;
        }

        isMeeting = parts[0] == "mtg";
        return true;
    }

    public static bool TryDecodeEventId(
        string? bridgeId,
        out string appointmentIdentity,
        out DateTimeOffset startUtc
    )
    {
        appointmentIdentity = string.Empty;
        startUtc = default;

        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            return false;
        }

        var parts = bridgeId.Split(':', 3, StringSplitOptions.None);
        if (parts.Length != 3 || parts[0] != "evt")
        {
            return false;
        }

        if (!TryDecode(parts[1], out appointmentIdentity))
        {
            return false;
        }

        return DateTimeOffset.TryParse(parts[2], out startUtc);
    }

    private static string B64(string value) =>
        Convert
            .ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool TryDecode(string encodedValue, out string decodedValue)
    {
        decodedValue = string.Empty;
        if (string.IsNullOrWhiteSpace(encodedValue))
        {
            return false;
        }

        var normalized = encodedValue.Replace('-', '+').Replace('_', '/');
        var remainder = normalized.Length % 4;
        if (remainder > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');
        }

        try
        {
            decodedValue = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
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
