namespace OpenClaw.MailBridge;

/// <summary>
/// Singleton implementation of <see cref="IOutlookApplicationProvider"/>. Holds the shared Outlook
/// <c>Application</c> COM object set by <see cref="OutlookScanner"/> on connect and cleared on
/// disconnect. The reference is read by the STA send path; reads and writes are guarded so the
/// send path observes a consistent reference. This type performs no live COM call itself.
/// </summary>
internal sealed class OutlookApplicationProvider : IOutlookApplicationProvider
{
    private readonly object _gate = new();
    private object? _application;

    /// <inheritdoc />
    public object? Application
    {
        get
        {
            lock (_gate)
            {
                return _application;
            }
        }
    }

    /// <inheritdoc />
    public void Set(object? application)
    {
        lock (_gate)
        {
            _application = application;
        }
    }
}
