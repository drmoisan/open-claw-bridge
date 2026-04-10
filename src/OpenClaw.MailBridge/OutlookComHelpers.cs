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
}
