namespace OpenClaw.Core.Agent;

/// <summary>
/// The closed set of Stage 0 result codes recorded in <see cref="ActionAuditRecord.ResultCode"/>
/// (issue #107, spec D2). Modeled as <see langword="const"/> strings rather than an enum so the
/// persisted <c>TEXT</c> values round-trip to SQLite without a mapping layer and Stage 2 (F18/F19)
/// can append reschedule codes without a contract or schema change. The store does not validate
/// membership; the worker (the only writer) enforces the closed set by construction.
/// </summary>
public static class ActionAuditResultCode
{
    /// <summary>The outbound send completed successfully.</summary>
    public const string Sent = "sent";

    /// <summary>The outbound send threw; the original exception still propagates.</summary>
    public const string SendFailed = "send_failed";

    /// <summary>The send was skipped because the dedupe store already recorded the action.</summary>
    public const string DedupeSkipped = "dedupe_skipped";

    /// <summary>The send was suppressed by the <c>SendEnabled</c> kill switch.</summary>
    public const string SendDisabled = "send_disabled";
}
