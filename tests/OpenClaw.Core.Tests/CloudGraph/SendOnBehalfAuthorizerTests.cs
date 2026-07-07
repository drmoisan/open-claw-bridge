using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Unit tests for the pure <see cref="SendOnBehalfAuthorizer.Authorize"/> function
/// covering all seven spec decision-table rows (F15, issue #119, spec "Decision
/// semantics"). Comparisons are trimmed and <see cref="StringComparison.OrdinalIgnoreCase"/>;
/// self-send dominates every allowlist; an empty allowlist denies every on-behalf send.
/// </summary>
[TestClass]
public sealed class SendOnBehalfAuthorizerTests
{
    private const string Principal = "paula@contoso.com";
    private const string Assistant = "amy@contoso.com";

    [TestMethod]
    public void Authorize_SelfSendEqualUpns_ReturnsAllowedSelf()
    {
        // Row 1: {p} == {a}; allowlist irrelevant (empty here).
        var decision = SendOnBehalfAuthorizer.Authorize(Principal, Principal, []);

        decision.Should().Be(SendAuthorizationDecision.AllowedSelf);
    }

    [TestMethod]
    public void Authorize_SelfSendDifferingOnlyByCase_ReturnsAllowedSelf()
    {
        // Row 1 (normalization): {p} and {a} differ only by case -> self-send.
        var decision = SendOnBehalfAuthorizer.Authorize(
            "Paula@Contoso.com",
            "paula@CONTOSO.com",
            []
        );

        decision.Should().Be(SendAuthorizationDecision.AllowedSelf);
    }

    [TestMethod]
    public void Authorize_SelfSendWithPrincipalInAllowlist_StillReturnsAllowedSelf()
    {
        // Self-send dominance: {p} == {a} even when the allowlist contains {p}.
        var decision = SendOnBehalfAuthorizer.Authorize(Principal, Principal, [Principal]);

        decision.Should().Be(SendAuthorizationDecision.AllowedSelf);
    }

    [TestMethod]
    public void Authorize_AllowlistedMember_ReturnsAllowedOnBehalf()
    {
        // Row 2: {p} != {a}, allowlist contains {p} exactly.
        var decision = SendOnBehalfAuthorizer.Authorize(Principal, Assistant, [Principal]);

        decision.Should().Be(SendAuthorizationDecision.AllowedOnBehalf);
    }

    [TestMethod]
    public void Authorize_CaseDifferingMember_ReturnsAllowedOnBehalf()
    {
        // Row 5: allowlist entry differs from {p} only by case.
        var decision = SendOnBehalfAuthorizer.Authorize(
            Principal,
            Assistant,
            ["PAULA@Contoso.COM"]
        );

        decision.Should().Be(SendAuthorizationDecision.AllowedOnBehalf);
    }

    [TestMethod]
    public void Authorize_WhitespacePaddedMember_ReturnsAllowedOnBehalf()
    {
        // Row 5: allowlist entry differs from {p} only by surrounding whitespace.
        var decision = SendOnBehalfAuthorizer.Authorize(
            Principal,
            Assistant,
            ["   paula@contoso.com  "]
        );

        decision.Should().Be(SendAuthorizationDecision.AllowedOnBehalf);
    }

    [TestMethod]
    public void Authorize_WhitespacePaddedPrincipal_ReturnsAllowedOnBehalf()
    {
        // Row 5: the principal itself is whitespace-padded; trimming normalizes it.
        var decision = SendOnBehalfAuthorizer.Authorize(
            "  paula@contoso.com ",
            Assistant,
            [Principal]
        );

        decision.Should().Be(SendAuthorizationDecision.AllowedOnBehalf);
    }

    [TestMethod]
    public void Authorize_EmptyAllowlistWithDifferingPrincipal_ReturnsDeniedNotAllowlisted()
    {
        // Row 3: {p} != {a}, allowlist empty -> fail-closed deny.
        var decision = SendOnBehalfAuthorizer.Authorize(Principal, Assistant, []);

        decision.Should().Be(SendAuthorizationDecision.DeniedNotAllowlisted);
    }

    [TestMethod]
    public void Authorize_NonMemberPrincipal_ReturnsDeniedNotAllowlisted()
    {
        // Row 4: {p} != {a}, allowlist non-empty but does not contain {p}.
        var decision = SendOnBehalfAuthorizer.Authorize(
            Principal,
            Assistant,
            ["someone-else@contoso.com", "another@contoso.com"]
        );

        decision.Should().Be(SendAuthorizationDecision.DeniedNotAllowlisted);
    }

    [TestMethod]
    public void Authorize_DuplicateAllowlistEntries_LeaveTheDecisionUnchanged()
    {
        // Row 7: duplicate entries are harmless (set semantics).
        var withDuplicates = new List<string> { Principal, Principal, "PAULA@contoso.com" };

        var decision = SendOnBehalfAuthorizer.Authorize(Principal, Assistant, withDuplicates);

        decision.Should().Be(SendAuthorizationDecision.AllowedOnBehalf);
    }
}
