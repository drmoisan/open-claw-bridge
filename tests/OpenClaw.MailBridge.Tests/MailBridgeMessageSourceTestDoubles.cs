namespace OpenClaw.MailBridge.Tests;

/// <summary>
/// Issue-#73 reflection-readable COM analogs used by <c>ComMessageSourceTests</c> and
/// <c>OutlookScannerMessageFieldsTests</c> to exercise the fail-soft SMTP sender-resolution chain in
/// <c>ComMessageSource</c> without live COM. Kept in a separate file so the shared
/// <c>MailBridgeRuntimeTestDoubles.cs</c> stays within the 500-line cap. The issue-#71
/// <c>FakeAddressEntry</c> (extended for #73) and recipient doubles remain in that file.
/// </summary>
internal sealed class FakePropertyAccessor
{
    public string? SmtpAddress { get; init; }

    // Late-bound analog: ComMessageSource invokes GetProperty(PR_SMTP_ADDRESS) reflectively.
    public object? GetProperty(string tag) => SmtpAddress;
}

/// <summary>
/// Reflection-readable analog of a COM <c>ExchangeUser</c> (issue #73): exposes the
/// <c>PrimarySmtpAddress</c> read by <c>ComMessageSource</c> on the GetExchangeUser path.
/// </summary>
internal sealed class FakeExchangeUser
{
    public string? PrimarySmtpAddress { get; init; }
}

/// <summary>
/// Reflection-readable analog of a COM message <c>Sender</c> (issue #73): exposes <c>Address</c>
/// (which may be a legacy Exchange DN) and an optional <see cref="FakeAddressEntry"/> used by the
/// fail-soft SMTP resolution chain in <c>ComMessageSource</c>.
/// </summary>
internal sealed class FakeSender
{
    public string? Address { get; init; }
    public FakeAddressEntry? AddressEntry { get; init; }
}
