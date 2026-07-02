using System;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.Agent;

namespace OpenClaw.Core.Tests.Agent;

/// <summary>
/// Property-based tests for the pure dedupe-key builder <see cref="SentActionKey"/>
/// (issue #101, AC-2): determinism, fixed component ordering, and distinctness for
/// colon-free component triples. CsCheck prints the failing seed on a <c>Sample</c>
/// failure, satisfying the determinism print-seed requirement.
/// </summary>
[TestClass]
public sealed class SentActionKeyPropertyTests
{
    // Colon-free, non-whitespace component: at least one character, drawn from a
    // range that excludes ':' and whitespace so the ordering and distinctness
    // properties hold by the builder's documented contract.
    private static readonly Gen<string> GenComponent = Gen.Char[
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@._-+"
        ]
        .Array[1, 20]
        .Select(chars => new string(chars));

    private static readonly Gen<(string Mailbox, string MessageId, string ActionType)> GenTriple =
        Gen.Select(GenComponent, GenComponent, GenComponent);

    [TestMethod]
    public void Build_IsDeterministic_SameTripleAlwaysYieldsSameKey()
    {
        GenTriple.Sample(
            t =>
            {
                var first = SentActionKey.Build(t.Mailbox, t.MessageId, t.ActionType);
                var second = SentActionKey.Build(t.Mailbox, t.MessageId, t.ActionType);

                first.Should().Be(second);
            },
            iter: 1000
        );
    }

    [TestMethod]
    public void Build_ColonFreeComponents_SplitYieldsComponentsInFixedOrder()
    {
        GenTriple.Sample(
            t =>
            {
                var key = SentActionKey.Build(t.Mailbox, t.MessageId, t.ActionType);

                var parts = key.Split(':');
                parts.Should().HaveCount(3);
                parts[0].Should().Be(t.Mailbox);
                parts[1].Should().Be(t.MessageId);
                parts[2].Should().Be(t.ActionType);
            },
            iter: 1000
        );
    }

    [TestMethod]
    public void Build_DistinctColonFreeTriples_YieldDistinctKeys()
    {
        Gen.Select(GenTriple, GenTriple)
            .Where(pair => pair.Item1 != pair.Item2)
            .Sample(
                pair =>
                {
                    var (a, b) = pair;
                    var keyA = SentActionKey.Build(a.Mailbox, a.MessageId, a.ActionType);
                    var keyB = SentActionKey.Build(b.Mailbox, b.MessageId, b.ActionType);

                    keyA.Should().NotBe(keyB);
                },
                iter: 1000
            );
    }
}
