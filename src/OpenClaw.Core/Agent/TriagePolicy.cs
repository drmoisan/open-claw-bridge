using System.Text.RegularExpressions;

namespace OpenClaw.Core.Agent;

/// <summary>
/// The value model holding the triage policy (D2), projected from
/// <see cref="AgentPolicyOptions"/>. Mirrors the <c>CONFIG</c> block in master
/// Section 9.2. Internal domains, VIP organizers, and protected categories are held as
/// case-insensitive sets; protected subject patterns are compiled regexes.
/// </summary>
public sealed class TriagePolicy
{
    private TriagePolicy(
        IReadOnlySet<string> internalDomains,
        IReadOnlySet<string> vipOrganizers,
        IReadOnlySet<string> protectedCategories,
        IReadOnlyList<Regex> protectedSubjectPatterns,
        int largeMeetingThreshold
    )
    {
        InternalDomains = internalDomains;
        VipOrganizers = vipOrganizers;
        ProtectedCategories = protectedCategories;
        ProtectedSubjectPatterns = protectedSubjectPatterns;
        LargeMeetingThreshold = largeMeetingThreshold;
    }

    /// <summary>Domains treated as internal, lowercased.</summary>
    public IReadOnlySet<string> InternalDomains { get; }

    /// <summary>VIP organizer addresses, lowercased.</summary>
    public IReadOnlySet<string> VipOrganizers { get; }

    /// <summary>Protected categories (case-insensitive).</summary>
    public IReadOnlySet<string> ProtectedCategories { get; }

    /// <summary>Compiled protected subject patterns.</summary>
    public IReadOnlyList<Regex> ProtectedSubjectPatterns { get; }

    /// <summary>Attendee count at or above which a meeting is large.</summary>
    public int LargeMeetingThreshold { get; }

    /// <summary>
    /// Projects an <see cref="AgentPolicyOptions"/> instance into a
    /// <see cref="TriagePolicy"/>. Email/domain/category sets are normalized to
    /// case-insensitive comparison; subject patterns are compiled with
    /// <see cref="RegexOptions.IgnoreCase"/>.
    /// </summary>
    /// <param name="options">The source options. Must not be null.</param>
    /// <returns>The projected triage policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static TriagePolicy FromOptions(AgentPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var internalDomains = new HashSet<string>(
            options.InternalDomains.Select(d => d.Trim().ToLowerInvariant()),
            StringComparer.Ordinal
        );
        var vipOrganizers = new HashSet<string>(
            options.VipOrganizers.Select(v => v.Trim().ToLowerInvariant()),
            StringComparer.Ordinal
        );
        var protectedCategories = new HashSet<string>(
            options.ProtectedCategories,
            StringComparer.OrdinalIgnoreCase
        );
        var patterns = options
            .ProtectedSubjectPatterns.Select(p => new Regex(
                p,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            ))
            .ToList();

        return new TriagePolicy(
            internalDomains,
            vipOrganizers,
            protectedCategories,
            patterns,
            options.LargeMeetingThreshold
        );
    }
}
