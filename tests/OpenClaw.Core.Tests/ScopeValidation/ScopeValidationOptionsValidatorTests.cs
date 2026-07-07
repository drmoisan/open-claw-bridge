using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// Pins the D6 rules of the pure <see cref="ScopeValidationOptionsValidator"/>: a disabled
/// section is always valid regardless of field values; when enabled both UPNs are required
/// and non-whitespace (each producing a key-named violation) and must differ
/// (OrdinalIgnoreCase); multiple faults are reported together (no short-circuit); and
/// violation messages name configuration keys without echoing configured values.
/// </summary>
[TestClass]
public sealed class ScopeValidationOptionsValidatorTests
{
    private const string InScopeKey = "InScopeTestMailboxUpn";
    private const string OutOfScopeKey = "OutOfScopeTestMailboxUpn";

    [TestMethod]
    public void Validate_Disabled_WithArbitraryInvalidFields_IsValid()
    {
        var options = new ScopeValidationOptions
        {
            Enabled = false,
            InScopeTestMailboxUpn = "same@contoso.com",
            OutOfScopeTestMailboxUpn = "same@contoso.com",
        };

        ScopeValidationOptionsValidator
            .Validate(options)
            .Should()
            .BeEmpty("a disabled section waives all other rules");
    }

    [TestMethod]
    public void Validate_Disabled_WithEmptyFields_IsValid()
    {
        var options = new ScopeValidationOptions { Enabled = false };

        ScopeValidationOptionsValidator.Validate(options).Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(null, DisplayName = "missing (null) in-scope UPN")]
    [DataRow("", DisplayName = "empty in-scope UPN")]
    [DataRow("   ", DisplayName = "whitespace-only in-scope UPN")]
    public void Validate_Enabled_MissingInScopeUpn_ViolatesInScopeKey(string? inScope)
    {
        var options = new ScopeValidationOptions
        {
            Enabled = true,
            InScopeTestMailboxUpn = inScope!,
            OutOfScopeTestMailboxUpn = "out-of-scope@contoso.com",
        };

        var violations = ScopeValidationOptionsValidator.Validate(options);

        violations.Should().ContainSingle();
        violations[0].Should().Contain(InScopeKey);
    }

    [DataTestMethod]
    [DataRow(null, DisplayName = "missing (null) out-of-scope UPN")]
    [DataRow("", DisplayName = "empty out-of-scope UPN")]
    [DataRow("   ", DisplayName = "whitespace-only out-of-scope UPN")]
    public void Validate_Enabled_MissingOutOfScopeUpn_ViolatesOutOfScopeKey(string? outOfScope)
    {
        var options = new ScopeValidationOptions
        {
            Enabled = true,
            InScopeTestMailboxUpn = "in-scope@contoso.com",
            OutOfScopeTestMailboxUpn = outOfScope!,
        };

        var violations = ScopeValidationOptionsValidator.Validate(options);

        violations.Should().ContainSingle();
        violations[0].Should().Contain(OutOfScopeKey);
    }

    [TestMethod]
    public void Validate_Enabled_EqualUpns_ViolatesDistinctness()
    {
        var options = new ScopeValidationOptions
        {
            Enabled = true,
            InScopeTestMailboxUpn = "shared@contoso.com",
            OutOfScopeTestMailboxUpn = "shared@contoso.com",
        };

        var violations = ScopeValidationOptionsValidator.Validate(options);

        violations.Should().ContainSingle();
        violations[0].Should().Contain("must differ");
    }

    [TestMethod]
    public void Validate_Enabled_CaseVariantEqualUpns_ViolatesDistinctness()
    {
        var options = new ScopeValidationOptions
        {
            Enabled = true,
            InScopeTestMailboxUpn = "Shared@Contoso.com",
            OutOfScopeTestMailboxUpn = "shared@contoso.COM",
        };

        var violations = ScopeValidationOptionsValidator.Validate(options);

        violations
            .Should()
            .ContainSingle(
                "distinctness is compared OrdinalIgnoreCase, so case-variant UPNs are equal"
            );
        violations[0].Should().Contain("must differ");
    }

    [TestMethod]
    public void Validate_Enabled_MultipleFaults_ReportsAllViolations()
    {
        // Both UPNs whitespace: two "required" violations; the distinctness check is
        // skipped because both values are missing (no short-circuit on the first fault).
        var options = new ScopeValidationOptions
        {
            Enabled = true,
            InScopeTestMailboxUpn = "   ",
            OutOfScopeTestMailboxUpn = "",
        };

        var violations = ScopeValidationOptionsValidator.Validate(options);

        violations.Should().HaveCount(2);
        violations.Should().Contain(v => v.Contains(InScopeKey));
        violations.Should().Contain(v => v.Contains(OutOfScopeKey));
    }

    [TestMethod]
    public void Validate_Enabled_ViolationMessages_DoNotEchoConfiguredValues()
    {
        var options = new ScopeValidationOptions
        {
            Enabled = true,
            InScopeTestMailboxUpn = "secret-in@contoso.com",
            OutOfScopeTestMailboxUpn = "secret-in@contoso.com",
        };

        var violations = ScopeValidationOptionsValidator.Validate(options);

        violations
            .Should()
            .OnlyContain(
                v => !v.Contains("secret-in@contoso.com"),
                "validation messages name config keys and never echo configured values"
            );
    }
}
