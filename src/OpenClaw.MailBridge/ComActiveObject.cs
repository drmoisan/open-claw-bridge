using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace OpenClaw.MailBridge;

/// <summary>
/// Resolves COM automation objects that are already registered in the running object table.
/// </summary>
internal class ComActiveObject
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        nint reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object obj
    );

    /// <summary>
    /// Attempts to retrieve a running COM object by ProgID.
    /// </summary>
    /// <param name="progId">Programmatic identifier for the COM server.</param>
    /// <returns>The active COM object when present; otherwise <see langword="null"/>.</returns>
    public virtual object? TryGet(string progId)
    {
        try
        {
            return TryGetCore(progId);
        }
        catch
        {
            // Treat lookup failures as a missing running instance because callers have a fallback path.
            return null;
        }
    }

    [ExcludeFromCodeCoverage]
    protected virtual object TryGetCore(string progId)
    {
        CLSIDFromProgID(progId, out var clsid);
        GetActiveObject(ref clsid, nint.Zero, out var obj);
        return obj;
    }

    /// <summary>
    /// Creates a new Outlook application instance and performs MAPI logon.
    /// </summary>
    /// <returns>The activated Outlook COM application object.</returns>
    public virtual object CreateAndLogonOutlook()
    {
        if (!IsWindowsPlatform())
        {
            throw new PlatformNotSupportedException("Outlook COM activation requires Windows.");
        }

        return CreateAndLogonOutlookCore();
    }

    protected virtual bool IsWindowsPlatform() => OperatingSystem.IsWindows();

    [ExcludeFromCodeCoverage]
    protected virtual object CreateAndLogonOutlookCore()
    {
        var t =
            Type.GetTypeFromProgID("Outlook.Application", false)
            ?? throw new InvalidOperationException("Outlook COM unavailable");
        var app =
            Activator.CreateInstance(t)
            ?? throw new InvalidOperationException("Outlook activation failed");
        var ns = t.InvokeMember(
            "GetNamespace",
            System.Reflection.BindingFlags.InvokeMethod,
            null,
            app,
            ["MAPI"]
        );
        // ShowDialog must be false so headless/scheduled-task execution never
        // blocks on the "Choose Profile" dialog.
        ns!
            .GetType()
            .InvokeMember(
                "Logon",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                ns,
                ["", "", false, false]
            );
        return app;
    }

    /// <summary>
    /// Releases a COM reference when the supplied object is a COM RCW.
    /// </summary>
    /// <param name="comObject">Object to release.</param>
    public virtual void Release(object? comObject)
    {
        if (comObject is null || !Marshal.IsComObject(comObject))
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(comObject);
        }
        catch (ArgumentException)
        {
            // The object was not a live COM wrapper anymore.
        }
    }

    /// <summary>
    /// Releases each supplied COM reference in reverse acquisition order.
    /// </summary>
    /// <param name="comObjects">Objects to release.</param>
    public virtual void ReleaseAll(params object?[] comObjects)
    {
        for (var i = comObjects.Length - 1; i >= 0; i--)
        {
            Release(comObjects[i]);
        }
    }
}
