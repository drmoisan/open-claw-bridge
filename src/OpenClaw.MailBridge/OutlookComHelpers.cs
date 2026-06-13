using System.Reflection;

namespace OpenClaw.MailBridge;

/// <summary>
/// COM reflection helpers extracted from <see cref="OutlookScanner"/> to keep both files
/// under the 500-line repo limit.  All members are <c>internal static</c> so the
/// scanner (and tests) can call them without exposing a public API surface.
/// </summary>
internal static class OutlookComHelpers
{
    internal static object? InvokeMember(object target, string memberName, params object?[] args) =>
        target
            .GetType()
            .InvokeMember(memberName, BindingFlags.InvokeMethod, binder: null, target, args);

    internal static object? GetMemberValue(object target, string memberName) =>
        target.GetType().InvokeMember(memberName, BindingFlags.GetProperty, null, target, null);

    internal static object? GetOptionalMemberValue(object target, string memberName)
    {
        try
        {
            return GetMemberValue(target, memberName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves an item from a COM collection by its 1-based index via the collection's
    /// <c>Item(index)</c> accessor, failing soft (returns <see langword="null"/>) on any COM error so
    /// a single unreadable element does not abort enumeration.
    /// </summary>
    internal static object? GetOptionalIndexedItem(object collection, int index)
    {
        try
        {
            return InvokeMember(collection, "Item", index);
        }
        catch
        {
            return null;
        }
    }

    internal static void SetMemberValue(object target, string memberName, object? value) =>
        target
            .GetType()
            .InvokeMember(
                memberName,
                BindingFlags.SetProperty,
                binder: null,
                target,
                args: [value]
            );

    internal static string? GetOptionalString(object target, string memberName)
    {
        try
        {
            return GetMemberValue(target, memberName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    internal static bool GetOptionalBool(object target, string memberName)
    {
        try
        {
            var value = GetMemberValue(target, memberName);
            return value switch
            {
                bool boolValue => boolValue,
                int intValue => intValue != 0,
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }

    internal static int? GetOptionalInt(object target, string memberName)
    {
        try
        {
            var value = GetMemberValue(target, memberName);
            return value is null ? null : Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    internal static DateTimeOffset? GetOptionalDateTimeOffset(object target, string memberName)
    {
        try
        {
            var value = GetMemberValue(target, memberName);
            return value switch
            {
                DateTimeOffset dto => dto,
                DateTime dateTime => new DateTimeOffset(dateTime.ToUniversalTime()),
                _ when DateTimeOffset.TryParse(value?.ToString(), out var parsed) => parsed,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves an optional UTC <see cref="DateTimeOffset"/> from a COM object member,
    /// treating <see cref="DateTimeKind.Unspecified"/> as already UTC to avoid double-shifting
    /// when Outlook's <c>StartUTC</c>/<c>EndUTC</c> properties return unspecified-kind values
    /// that already carry a UTC timestamp.
    /// </summary>
    internal static DateTimeOffset? GetOptionalUtcDateTimeOffset(object target, string memberName)
    {
        try
        {
            var value = GetMemberValue(target, memberName);
            return value switch
            {
                DateTimeOffset dto => dto.ToUniversalTime(),
                DateTime { Kind: DateTimeKind.Utc } dateTime => new DateTimeOffset(dateTime),
                DateTime { Kind: DateTimeKind.Local } dateTime => new DateTimeOffset(
                    dateTime.ToUniversalTime()
                ),
                DateTime dateTime => new DateTimeOffset(dateTime, TimeSpan.Zero),
                _ when DateTimeOffset.TryParse(value?.ToString(), out var parsed) => parsed,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }
}
