namespace OpenClaw.Core.Agent;

/// <summary>
/// The value model holding the owner-specific scheduling policy (D3), projected from
/// <see cref="AgentPolicyOptions"/>. Mirrors the priority lists in master Section 10.1.
/// Address lists are held as case-insensitive (lowercased) sets.
/// </summary>
public sealed class OwnerSchedulingPolicy
{
    private OwnerSchedulingPolicy(
        IReadOnlySet<string> vipEmails,
        IReadOnlySet<string> directReports,
        IReadOnlySet<string> priority1,
        IReadOnlySet<string> priority2,
        IReadOnlySet<string> priority3,
        string internalDomain,
        string emblemEmailDomain
    )
    {
        VipEmails = vipEmails;
        DirectReports = directReports;
        Priority1 = priority1;
        Priority2 = priority2;
        Priority3 = priority3;
        InternalDomain = internalDomain;
        EmblemEmailDomain = emblemEmailDomain;
    }

    /// <summary>VIP sender addresses (Priority 0), lowercased.</summary>
    public IReadOnlySet<string> VipEmails { get; }

    /// <summary>Direct-report sender addresses, lowercased.</summary>
    public IReadOnlySet<string> DirectReports { get; }

    /// <summary>Explicit Priority 1 sender addresses, lowercased.</summary>
    public IReadOnlySet<string> Priority1 { get; }

    /// <summary>Explicit Priority 2 sender addresses, lowercased.</summary>
    public IReadOnlySet<string> Priority2 { get; }

    /// <summary>Explicit Priority 3 sender addresses, lowercased.</summary>
    public IReadOnlySet<string> Priority3 { get; }

    /// <summary>The owner's internal domain, lowercased.</summary>
    public string InternalDomain { get; }

    /// <summary>The non-VIP partner domain treated as Priority 1, lowercased.</summary>
    public string EmblemEmailDomain { get; }

    /// <summary>
    /// Projects an <see cref="AgentPolicyOptions"/> instance into an
    /// <see cref="OwnerSchedulingPolicy"/>, lowercasing all address and domain values.
    /// </summary>
    /// <param name="options">The source options. Must not be null.</param>
    /// <returns>The projected owner scheduling policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static OwnerSchedulingPolicy FromOptions(AgentPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new OwnerSchedulingPolicy(
            LowerSet(options.VipEmails),
            LowerSet(options.DirectReports),
            LowerSet(options.Priority1),
            LowerSet(options.Priority2),
            LowerSet(options.Priority3),
            options.InternalDomain.Trim().ToLowerInvariant(),
            options.EmblemEmailDomain.Trim().ToLowerInvariant()
        );
    }

    private static IReadOnlySet<string> LowerSet(IReadOnlyList<string> values) =>
        new HashSet<string>(
            values.Select(v => v.Trim().ToLowerInvariant()),
            StringComparer.Ordinal
        );
}
