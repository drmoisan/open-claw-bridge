using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.CloudGraph;

namespace OpenClaw.Core.Tests.CloudGraph;

/// <summary>
/// Unit tests for <see cref="GraphAdapterOptionsValidator"/>: a disabled adapter is
/// always valid; when enabled, every rule (UPNs, BaseUrl scheme/absoluteness, and each
/// numeric bound) produces exactly one named violation, and a fully valid enabled
/// configuration passes.
/// </summary>
[TestClass]
public sealed class GraphAdapterOptionsValidatorTests
{
    /// <summary>Builds an enabled options instance that satisfies every rule.</summary>
    private static GraphAdapterOptions ValidEnabledOptions() =>
        new()
        {
            Enabled = true,
            PrincipalMailboxUpn = "principal@contoso.com",
            AssistantMailboxUpn = "assistant@contoso.com",
        };

    [TestMethod]
    public void Validate_NullOptions_Throws()
    {
        var act = () => GraphAdapterOptionsValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Validate_DisabledWithDefaults_ReturnsNoViolations()
    {
        // Disabled adapter: empty UPNs and all defaults must still validate clean.
        var options = new GraphAdapterOptions();

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations.Should().BeEmpty("a disabled adapter is always valid");
    }

    [TestMethod]
    public void Validate_DisabledWithInvalidValues_ReturnsNoViolations()
    {
        // Even out-of-range values pass while Enabled is false (rules are gated).
        var options = new GraphAdapterOptions
        {
            Enabled = false,
            BaseUrl = "not-a-uri",
            PageSize = 0,
            MaxPages = 0,
            MaxAttempts = 0,
            BaseDelaySeconds = 0,
            MaxDelaySeconds = -1,
            AvailabilityViewIntervalMinutes = 0,
        };

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations.Should().BeEmpty("validation rules apply only when Enabled is true");
    }

    [TestMethod]
    public void Validate_EnabledWithDefaults_FailsOnBothEmptyUpns()
    {
        // Enabled with factory defaults: only the two required UPNs are missing.
        var options = new GraphAdapterOptions { Enabled = true };

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .BeEquivalentTo(
                "PrincipalMailboxUpn is required and must be non-whitespace.",
                "AssistantMailboxUpn is required and must be non-whitespace."
            );
    }

    [TestMethod]
    public void Validate_EnabledFullyValid_ReturnsNoViolations()
    {
        var violations = GraphAdapterOptionsValidator.Validate(ValidEnabledOptions());

        violations.Should().BeEmpty();
    }

    [TestMethod]
    public void Validate_WhitespacePrincipalUpn_ProducesExactlyOnePrincipalViolation()
    {
        var options = ValidEnabledOptions();
        options.PrincipalMailboxUpn = "   ";

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("PrincipalMailboxUpn is required and must be non-whitespace.");
    }

    [TestMethod]
    public void Validate_WhitespaceAssistantUpn_ProducesExactlyOneAssistantViolation()
    {
        var options = ValidEnabledOptions();
        options.AssistantMailboxUpn = "";

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("AssistantMailboxUpn is required and must be non-whitespace.");
    }

    [DataTestMethod]
    [DataRow("http://graph.microsoft.com/v1.0/", DisplayName = "non-https scheme")]
    [DataRow("graph.microsoft.com/v1.0/", DisplayName = "relative URI")]
    [DataRow("   ", DisplayName = "whitespace")]
    public void Validate_InvalidBaseUrl_ProducesExactlyOneBaseUrlViolation(string baseUrl)
    {
        var options = ValidEnabledOptions();
        options.BaseUrl = baseUrl;

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("BaseUrl must be an absolute https URI.");
    }

    [DataTestMethod]
    [DataRow(0, DisplayName = "below lower bound")]
    [DataRow(1001, DisplayName = "above upper bound")]
    public void Validate_PageSizeOutOfRange_ProducesExactlyOnePageSizeViolation(int pageSize)
    {
        var options = ValidEnabledOptions();
        options.PageSize = pageSize;

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("PageSize must be between 1 and 1000 inclusive.");
    }

    [DataTestMethod]
    [DataRow(1, DisplayName = "lower edge")]
    [DataRow(1000, DisplayName = "upper edge")]
    public void Validate_PageSizeAtEdges_Passes(int pageSize)
    {
        var options = ValidEnabledOptions();
        options.PageSize = pageSize;

        GraphAdapterOptionsValidator.Validate(options).Should().BeEmpty();
    }

    [TestMethod]
    public void Validate_MaxPagesBelowOne_ProducesExactlyOneMaxPagesViolation()
    {
        var options = ValidEnabledOptions();
        options.MaxPages = 0;

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations.Should().ContainSingle().Which.Should().Be("MaxPages must be at least 1.");
    }

    [TestMethod]
    public void Validate_MaxPagesAtOne_Passes()
    {
        var options = ValidEnabledOptions();
        options.MaxPages = 1;

        GraphAdapterOptionsValidator.Validate(options).Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(0, DisplayName = "below lower bound")]
    [DataRow(11, DisplayName = "above upper bound")]
    public void Validate_MaxAttemptsOutOfRange_ProducesExactlyOneMaxAttemptsViolation(
        int maxAttempts
    )
    {
        var options = ValidEnabledOptions();
        options.MaxAttempts = maxAttempts;

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("MaxAttempts must be between 1 and 10 inclusive.");
    }

    [DataTestMethod]
    [DataRow(1, DisplayName = "lower edge")]
    [DataRow(10, DisplayName = "upper edge")]
    public void Validate_MaxAttemptsAtEdges_Passes(int maxAttempts)
    {
        var options = ValidEnabledOptions();
        options.MaxAttempts = maxAttempts;

        GraphAdapterOptionsValidator.Validate(options).Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(0, DisplayName = "zero")]
    [DataRow(-1, DisplayName = "negative")]
    public void Validate_BaseDelayNotPositive_ProducesExactlyOneBaseDelayViolation(int baseDelay)
    {
        var options = ValidEnabledOptions();
        options.BaseDelaySeconds = baseDelay;

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("BaseDelaySeconds must be greater than zero.");
    }

    [TestMethod]
    public void Validate_MaxDelayBelowBaseDelay_ProducesExactlyOneMaxDelayViolation()
    {
        var options = ValidEnabledOptions();
        options.BaseDelaySeconds = 5;
        options.MaxDelaySeconds = 4;

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("MaxDelaySeconds must be greater than or equal to BaseDelaySeconds.");
    }

    [TestMethod]
    public void Validate_MaxDelayEqualToBaseDelay_Passes()
    {
        var options = ValidEnabledOptions();
        options.BaseDelaySeconds = 5;
        options.MaxDelaySeconds = 5;

        GraphAdapterOptionsValidator.Validate(options).Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(4, DisplayName = "below lower bound")]
    [DataRow(1441, DisplayName = "above upper bound")]
    public void Validate_AvailabilityViewIntervalOutOfRange_ProducesExactlyOneViolation(int minutes)
    {
        var options = ValidEnabledOptions();
        options.AvailabilityViewIntervalMinutes = minutes;

        var violations = GraphAdapterOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("AvailabilityViewIntervalMinutes must be between 5 and 1440 inclusive.");
    }

    [DataTestMethod]
    [DataRow(5, DisplayName = "lower edge")]
    [DataRow(1440, DisplayName = "upper edge")]
    public void Validate_AvailabilityViewIntervalAtEdges_Passes(int minutes)
    {
        var options = ValidEnabledOptions();
        options.AvailabilityViewIntervalMinutes = minutes;

        GraphAdapterOptionsValidator.Validate(options).Should().BeEmpty();
    }
}
