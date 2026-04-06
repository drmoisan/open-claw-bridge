using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace OpenClaw.MailBridge;

/// <summary>
/// Resolves COM automation objects that are already registered in the running object table.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class ComActiveObject
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
    public static object? TryGet(string progId)
    {
        try
        {
            CLSIDFromProgID(progId, out var clsid);
            GetActiveObject(ref clsid, nint.Zero, out var obj);
            return obj;
        }
        catch
        {
            // Treat lookup failures as a missing running instance because callers have a fallback path.
            return null;
        }
    }
}
