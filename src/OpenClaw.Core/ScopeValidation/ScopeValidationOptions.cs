namespace OpenClaw.Core.ScopeValidation;

/// <summary>
/// Scope-boundary validation configuration (spec D6). Bound from the
/// <c>OpenClaw:ScopeValidation</c> configuration section. This type is a plain options
/// bag; all validation lives in <see cref="ScopeValidationOptionsValidator"/>. Opt-in:
/// when <see cref="Enabled"/> is <c>false</c> the section is inert and all other rules are
/// waived, so the default (local/Stage-0) configuration is unaffected.
/// </summary>
public sealed class ScopeValidationOptions
{
    /// <summary>The configuration section this options bag binds from.</summary>
    public const string SectionName = "OpenClaw:ScopeValidation";

    /// <summary>
    /// Opt-in switch; <c>false</c> registers nothing and waives all other rules. Env
    /// binding: <c>OpenClaw__ScopeValidation__Enabled</c>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The in-scope test mailbox UPN (master §13 Step 1 <c>IN_SCOPE_TEST_MAILBOX</c>).
    /// Required, non-whitespace when <see cref="Enabled"/>. Env binding:
    /// <c>OpenClaw__ScopeValidation__InScopeTestMailboxUpn</c>.
    /// </summary>
    public string InScopeTestMailboxUpn { get; set; } = string.Empty;

    /// <summary>
    /// The out-of-scope test mailbox UPN (master §13 Step 1 <c>OUT_OF_SCOPE_TEST_MAILBOX</c>).
    /// Required, non-whitespace, and must differ from <see cref="InScopeTestMailboxUpn"/>
    /// (OrdinalIgnoreCase) when <see cref="Enabled"/>. Env binding:
    /// <c>OpenClaw__ScopeValidation__OutOfScopeTestMailboxUpn</c>.
    /// </summary>
    public string OutOfScopeTestMailboxUpn { get; set; } = string.Empty;
}
