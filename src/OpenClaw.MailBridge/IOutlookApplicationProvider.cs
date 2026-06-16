namespace OpenClaw.MailBridge;

/// <summary>
/// Shares the single live Outlook <c>Application</c> COM object between the scan path
/// (<see cref="OutlookScanner"/>, which owns connect/disconnect) and the send path
/// (<c>OutlookComMailSender</c>). The held reference is an opaque <see cref="object"/> so no
/// Outlook COM type crosses this seam; COM interop remains confined to
/// <c>OpenClaw.MailBridge</c>.
/// </summary>
internal interface IOutlookApplicationProvider
{
    /// <summary>
    /// The current live Outlook <c>Application</c> COM object, or <see langword="null"/> when no
    /// Outlook session is connected. The send path must treat a <see langword="null"/> value as
    /// "Outlook unavailable" and fail fast.
    /// </summary>
    object? Application { get; }

    /// <summary>
    /// Sets (on connect) or clears (with <see langword="null"/>, on disconnect/teardown) the shared
    /// Outlook <c>Application</c> reference. Called only by the owner of the COM session
    /// (<see cref="OutlookScanner"/>).
    /// </summary>
    /// <param name="application">The live Outlook <c>Application</c>, or <see langword="null"/> to clear.</param>
    void Set(object? application);
}
