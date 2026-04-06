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
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Outlook COM activation requires Windows.");
        }

        return CreateAndLogonOutlookCore();
    }

    [ExcludeFromCodeCoverage]
    protected virtual object CreateAndLogonOutlookCore()
    {
        var t =
            Type.GetTypeFromProgID("Outlook.Application", false)
            ?? throw new InvalidOperationException("Outlook COM unavailable");
        var app =
            Activator.CreateInstance(t)
            ?? throw new InvalidOperationException("Outlook activation failed");
        var ns = t.InvokeMember("GetNamespace", System.Reflection.BindingFlags.InvokeMethod, null, app, ["MAPI"]);
        ns!
            .GetType()
            .InvokeMember(
                "Logon",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                ns,
                ["", "", Type.Missing, Type.Missing]
            );
        return app;
    }
}
