namespace OpenClaw.Core.CloudGraph;

/// <summary>
/// Microsoft Graph adapter configuration (D8). Bound from the
/// <c>OpenClaw:GraphAdapter</c> configuration section. This type is a plain options
/// bag; all validation lives in <see cref="GraphAdapterOptionsValidator"/>.
/// </summary>
public sealed class GraphAdapterOptions
{
    /// <summary>
    /// Backend selector; <c>false</c> keeps the local HTTP client registration.
    /// Env binding: <c>OpenClaw__GraphAdapter__Enabled</c>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The principal mailbox UPN (<c>{p}</c> in all read routes; <c>from</c> on send).
    /// Required, non-whitespace when <see cref="Enabled"/>. Env binding:
    /// <c>OpenClaw__GraphAdapter__PrincipalMailboxUpn</c>.
    /// </summary>
    public string PrincipalMailboxUpn { get; set; } = string.Empty;

    /// <summary>
    /// The assistant mailbox UPN (<c>{a}</c>; <c>sendMail</c> submits through it).
    /// Required, non-whitespace when <see cref="Enabled"/>. Env binding:
    /// <c>OpenClaw__GraphAdapter__AssistantMailboxUpn</c>.
    /// </summary>
    public string AssistantMailboxUpn { get; set; } = string.Empty;

    /// <summary>
    /// The Graph REST base URL; must be an absolute <c>https</c> URI. Override exists
    /// only for national-cloud endpoints. Env binding:
    /// <c>OpenClaw__GraphAdapter__BaseUrl</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "https://graph.microsoft.com/v1.0/";

    /// <summary>
    /// Value of the <c>Prefer: outlook.timezone</c> header applied to read routes so
    /// time rendering is deterministic. Env binding:
    /// <c>OpenClaw__GraphAdapter__PreferredTimeZone</c>.
    /// </summary>
    public string PreferredTimeZone { get; set; } = "UTC";

    /// <summary>
    /// Per-page <c>$top</c> for list routes (1-1000). Env binding:
    /// <c>OpenClaw__GraphAdapter__PageSize</c>.
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Maximum number of <c>@odata.nextLink</c> pages followed per list call
    /// (&gt;= 1); the determinism/runaway bound (D3). Env binding:
    /// <c>OpenClaw__GraphAdapter__MaxPages</c>.
    /// </summary>
    public int MaxPages { get; set; } = 10;

    /// <summary>
    /// Total request attempts including the first (1-10) for retryable statuses
    /// (D6). Env binding: <c>OpenClaw__GraphAdapter__MaxAttempts</c>.
    /// </summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>
    /// Exponential backoff base delay in seconds (&gt; 0); attempt <c>n</c> waits
    /// <c>BaseDelaySeconds * 2^(n-1)</c> when no <c>Retry-After</c> header is present
    /// (D6). Env binding: <c>OpenClaw__GraphAdapter__BaseDelaySeconds</c>.
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Backoff delay cap in seconds (&gt;= <see cref="BaseDelaySeconds"/>). Env
    /// binding: <c>OpenClaw__GraphAdapter__MaxDelaySeconds</c>.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 30;

    /// <summary>
    /// The <c>getSchedule</c> <c>availabilityViewInterval</c> in minutes (5-1440).
    /// Env binding: <c>OpenClaw__GraphAdapter__AvailabilityViewIntervalMinutes</c>.
    /// </summary>
    public int AvailabilityViewIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// The allowlist of principal mailbox UPNs the assistant mailbox (<c>{a}</c>) may
    /// send on behalf of (F15, issue #119). Bound from indexed configuration keys
    /// (<c>OpenClaw__GraphAdapter__AllowedPrincipalMailboxUpns__0</c>,
    /// <c>__1</c>, ...). This is a get-only collection so the configuration binder adds
    /// bound entries to the initialized list.
    /// </summary>
    /// <remarks>
    /// Fail-closed-empty semantics: an empty or absent allowlist denies every
    /// on-behalf send (<c>{p} != {a}</c>); self-send (<c>{p} == {a}</c>) is unaffected
    /// by the allowlist and always permitted. Membership is decided by
    /// <see cref="SendOnBehalfAuthorizer.Authorize"/> with trimmed,
    /// <see cref="System.StringComparison.OrdinalIgnoreCase"/> comparison.
    /// </remarks>
    public IList<string> AllowedPrincipalMailboxUpns { get; } = new List<string>();
}
