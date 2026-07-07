using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// CsCheck property tests for the pure <see cref="SendOnBehalfAuthorizer.Authorize"/>
/// function (F15, issue #119, spec S3), satisfying the T1 obligation of at least one
/// property test per pure function. Four properties are established: case-invariance,
/// deny-completeness, membership soundness, and self-send dominance. CsCheck prints the
/// failing seed on a <c>Sample</c> failure, satisfying the reproducibility requirement.
/// </summary>
[TestClass]
public sealed class SendOnBehalfAuthorizerPropertyTests
{
    private const int Iterations = 1000;

    private static readonly string[] Pads = ["", " ", "  ", "\t"];

    /// <summary>A UPN with a random lowercase-alphanumeric local part and a fixed domain.</summary>
    private static readonly Gen<string> GenUpn = Gen.Char["abcdefghijklmnopqrstuvwxyz0123456789"]
        .Array[1, 10]
        .Select(chars => new string(chars) + "@contoso.com");

    /// <summary>Zero to five UPN entries (models an arbitrary allowlist, including empty).</summary>
    private static readonly Gen<string[]> GenAllowlist = GenUpn.Array[0, 5];

    [TestMethod]
    public void Authorize_CaseInvariance_RandomCasingNeverChangesTheDecision()
    {
        var gen =
            from principal in GenUpn
            from assistant in GenUpn
            from allowlist in GenAllowlist
            from recasedPrincipal in Recased(principal)
            from recasedAllowlist in RecasedList(allowlist)
            select (principal, assistant, allowlist, recasedPrincipal, recasedAllowlist);

        gen.Sample(
            t =>
            {
                var baseline = SendOnBehalfAuthorizer.Authorize(
                    t.principal,
                    t.assistant,
                    t.allowlist
                );
                var recased = SendOnBehalfAuthorizer.Authorize(
                    t.recasedPrincipal,
                    t.assistant,
                    t.recasedAllowlist
                );

                recased
                    .Should()
                    .Be(baseline, "casing of the principal and allowlist entries is irrelevant");
            },
            iter: Iterations
        );
    }

    [TestMethod]
    public void Authorize_DenyCompleteness_AllowlistExcludingDifferingPrincipal_AlwaysDenies()
    {
        var gen =
            from principal in GenUpn
            from assistant in GenUpn
            where !TrimEqual(principal, assistant)
            from allowlist in GenAllowlist
            select (principal, assistant, allowlist);

        gen.Sample(
            t =>
            {
                // Exclude any entry equal to the principal so the allowlist genuinely omits it.
                var withoutPrincipal = t
                    .allowlist.Where(entry => !TrimEqual(entry, t.principal))
                    .ToArray();

                var decision = SendOnBehalfAuthorizer.Authorize(
                    t.principal,
                    t.assistant,
                    withoutPrincipal
                );

                decision
                    .Should()
                    .Be(
                        SendAuthorizationDecision.DeniedNotAllowlisted,
                        "a differing principal absent from the allowlist is always denied"
                    );
            },
            iter: Iterations
        );
    }

    [TestMethod]
    public void Authorize_MembershipSoundness_InsertingThePrincipal_AlwaysAllowsOnBehalf()
    {
        var gen =
            from principal in GenUpn
            from assistant in GenUpn
            where !TrimEqual(principal, assistant)
            from allowlist in GenAllowlist
            from recased in Recased(principal)
            from variant in Padded(recased)
            from position in Gen.Int[0, allowlist.Length]
            select (principal, assistant, allowlist, variant, position);

        gen.Sample(
            t =>
            {
                var withPrincipal = t.allowlist.ToList();
                withPrincipal.Insert(t.position, t.variant);

                var decision = SendOnBehalfAuthorizer.Authorize(
                    t.principal,
                    t.assistant,
                    withPrincipal
                );

                decision
                    .Should()
                    .Be(
                        SendAuthorizationDecision.AllowedOnBehalf,
                        "the principal inserted in any casing or padding is a member"
                    );
            },
            iter: Iterations
        );
    }

    [TestMethod]
    public void Authorize_SelfSendDominance_PrincipalEqualsAssistant_AlwaysAllowsSelf()
    {
        var gen =
            from principal in GenUpn
            from allowlist in GenAllowlist
            from assistantCasing in Recased(principal)
            select (principal, assistant: assistantCasing, allowlist);

        gen.Sample(
            t =>
            {
                var decision = SendOnBehalfAuthorizer.Authorize(
                    t.principal,
                    t.assistant,
                    t.allowlist
                );

                decision
                    .Should()
                    .Be(
                        SendAuthorizationDecision.AllowedSelf,
                        "self-send dominates every allowlist regardless of contents"
                    );
            },
            iter: Iterations
        );
    }

    private static bool TrimEqual(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Randomly re-cases each character of <paramref name="value"/>.</summary>
    private static Gen<string> Recased(string value) =>
        Gen
            .Bool.Array[value.Length, value.Length]
            .Select(flags =>
            {
                var chars = value.ToCharArray();
                for (var i = 0; i < chars.Length; i++)
                {
                    chars[i] = flags[i]
                        ? char.ToUpperInvariant(chars[i])
                        : char.ToLowerInvariant(chars[i]);
                }

                return new string(chars);
            });

    /// <summary>Surrounds <paramref name="value"/> with randomly chosen padding.</summary>
    private static Gen<string> Padded(string value) =>
        from left in Gen.Int[0, Pads.Length - 1]
        from right in Gen.Int[0, Pads.Length - 1]
        select Pads[left] + value + Pads[right];

    /// <summary>Re-cases every entry of <paramref name="entries"/> independently.</summary>
    private static Gen<string[]> RecasedList(string[] entries)
    {
        Gen<List<string>> accumulator = Gen.Const(new List<string>());
        foreach (var entry in entries)
        {
            var current = accumulator;
            accumulator =
                from list in current
                from recased in Recased(entry)
                select Append(list, recased);
        }

        return accumulator.Select(list => list.ToArray());
    }

    private static List<string> Append(List<string> list, string item)
    {
        var copy = new List<string>(list) { item };
        return copy;
    }
}
