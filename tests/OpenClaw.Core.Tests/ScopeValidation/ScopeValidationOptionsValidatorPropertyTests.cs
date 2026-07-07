using CsCheck;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenClaw.Core.ScopeValidation;

namespace OpenClaw.Core.Tests.ScopeValidation;

/// <summary>
/// CsCheck property tests for the pure <see cref="ScopeValidationOptionsValidator"/>:
/// (a) for any generated UPN field values, a disabled section yields zero violations; and
/// (b) for any generated options, the verdict is deterministic — the same input produces
/// the same violation list. Failing seeds are reported by CsCheck's default output.
/// </summary>
[TestClass]
public sealed class ScopeValidationOptionsValidatorPropertyTests
{
    // A vocabulary spanning null, empty, whitespace, and distinct/overlapping UPNs so the
    // generated space covers every rule branch.
    private static readonly string?[] UpnValues =
    [
        null,
        "",
        "   ",
        "in-scope@contoso.com",
        "out-of-scope@contoso.com",
        "IN-SCOPE@CONTOSO.COM",
    ];

    private static readonly Gen<ScopeValidationOptions> OptionsGen = Gen.Select(
        Gen.Bool,
        Gen.Int[0, UpnValues.Length - 1],
        Gen.Int[0, UpnValues.Length - 1],
        (enabled, inIndex, outIndex) =>
            new ScopeValidationOptions
            {
                Enabled = enabled,
                InScopeTestMailboxUpn = UpnValues[inIndex]!,
                OutOfScopeTestMailboxUpn = UpnValues[outIndex]!,
            }
    );

    [TestMethod]
    public void Validate_Disabled_AlwaysYieldsZeroViolations()
    {
        Gen.Select(Gen.Int[0, UpnValues.Length - 1], Gen.Int[0, UpnValues.Length - 1])
            .Sample(indexes =>
            {
                var (inIndex, outIndex) = indexes;
                var options = new ScopeValidationOptions
                {
                    Enabled = false,
                    InScopeTestMailboxUpn = UpnValues[inIndex]!,
                    OutOfScopeTestMailboxUpn = UpnValues[outIndex]!,
                };

                ScopeValidationOptionsValidator
                    .Validate(options)
                    .Should()
                    .BeEmpty("a disabled section is always valid regardless of field values");
            });
    }

    [TestMethod]
    public void Validate_IsDeterministic_ForAnyOptions()
    {
        OptionsGen.Sample(options =>
        {
            var first = ScopeValidationOptionsValidator.Validate(options);
            var second = ScopeValidationOptionsValidator.Validate(options);

            second
                .Should()
                .Equal(first, "the validator is pure; the same input yields the same list");
        });
    }
}
