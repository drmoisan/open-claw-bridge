using System.Text.RegularExpressions;

namespace OpenClaw.Core.Agent;

/// <summary>
/// Pure helper methods used by <see cref="MeetingContextNormalizer"/>. Ported from the
/// helper functions in master Section 9.2 (<c>normalizeEmail</c>, <c>emailOf</c>,
/// <c>stripHtml</c>, <c>normalizeAttendees</c>).
/// </summary>
public static partial class MeetingContextNormalizer
{
    // The HTML-stripping regexes mirror the master Section 9.2 stripHtml sequence:
    // remove <style>/<script> blocks, remove remaining tags, decode a small set of
    // entities, then collapse whitespace.
    [GeneratedRegex(@"<style[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleBlockRegex();

    [GeneratedRegex(@"<script[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlockRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"&nbsp;", RegexOptions.IgnoreCase)]
    private static partial Regex NonBreakingSpaceRegex();

    [GeneratedRegex(@"&amp;", RegexOptions.IgnoreCase)]
    private static partial Regex AmpersandRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>Trims and lowercases an email-like value; null becomes empty.</summary>
    public static string NormalizeEmail(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Extracts and normalizes the email address from an attendee, or empty.</summary>
    public static string EmailOf(AttendeeDto? attendee) => NormalizeEmail(attendee?.Email);

    /// <summary>
    /// Strips HTML markup from <paramref name="value"/> per master Section 9.2:
    /// removes style/script blocks and tags, decodes <c>&amp;nbsp;</c> and
    /// <c>&amp;amp;</c>, and collapses whitespace.
    /// </summary>
    public static string StripHtml(string? value)
    {
        var text = value ?? string.Empty;
        text = StyleBlockRegex().Replace(text, " ");
        text = ScriptBlockRegex().Replace(text, " ");
        text = TagRegex().Replace(text, " ");
        text = NonBreakingSpaceRegex().Replace(text, " ");
        text = AmpersandRegex().Replace(text, "&");
        text = WhitespaceRegex().Replace(text, " ");
        return text.Trim();
    }

    /// <summary>
    /// Partitions attendees into required/optional/resource lists, normalizing emails
    /// and skipping empties, per master Section 9.2 <c>normalizeAttendees</c>. The three
    /// returned lists preserve input order; the caller concatenates them for
    /// <c>AllAttendees</c>.
    /// </summary>
    internal static (
        IReadOnlyList<string> Required,
        IReadOnlyList<string> Optional,
        IReadOnlyList<string> Resource,
        IReadOnlyList<string> All
    ) NormalizeAttendees(
        IReadOnlyList<AttendeeDto> required,
        IReadOnlyList<AttendeeDto> optional,
        IReadOnlyList<AttendeeDto> resource
    )
    {
        var requiredEmails = NonEmptyEmails(required);
        var optionalEmails = NonEmptyEmails(optional);
        var resourceEmails = NonEmptyEmails(resource);
        var all = new List<string>(
            requiredEmails.Count + optionalEmails.Count + resourceEmails.Count
        );
        all.AddRange(requiredEmails);
        all.AddRange(optionalEmails);
        all.AddRange(resourceEmails);
        return (requiredEmails, optionalEmails, resourceEmails, all);
    }

    private static List<string> NonEmptyEmails(IReadOnlyList<AttendeeDto> attendees)
    {
        var result = new List<string>(attendees.Count);
        foreach (var attendee in attendees)
        {
            var email = EmailOf(attendee);
            if (email.Length > 0)
            {
                result.Add(email);
            }
        }

        return result;
    }
}
